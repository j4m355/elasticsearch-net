﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using CodeGeneration.LowLevelClient.Domain;
using CodeGeneration.LowLevelClient.Overrides.Allow404;
using CodeGeneration.LowLevelClient.Overrides.Descriptors;
using CsQuery;
using CsQuery.ExtensionMethods.Internal;
using Newtonsoft.Json;
using Xipton.Razor;
using CodeGeneration.LowLevelClient.Overrides.Global;
using Xipton.Razor.Config;

namespace CodeGeneration.LowLevelClient
{
	public static class ApiGenerator
	{
		private readonly static string _nestFolder = @"..\..\..\src\Nest\";
		private readonly static string _esNetFolder = @"..\..\..\src\Elasticsearch.Net\";
		private readonly static string _viewFolder = @"Views\";
		private readonly static string _apiEndpointsFolder = @"ApiEndpoints\";
		private static readonly RazorMachine _razorMachine;

		private static readonly string _version = "2.0";
		private static List<string> ApiListings = new List<string>
		{
			"https://github.com/elastic/elasticsearch/tree/{version}/rest-api-spec/src/main/resources/rest-api-spec/api",
			"https://github.com/elastic/elasticsearch/tree/{version}/plugins/delete-by-query/rest-api-spec/api"
		};

		private static readonly Assembly _assembly;

		static ApiGenerator()
		{
			_razorMachine = new RazorMachine(references: new[] { "Xipton.Razor.dll" });
			_assembly = typeof(ApiGenerator).Assembly;
		}
		public static string PascalCase(string s)
		{
			var textInfo = new CultureInfo("en-US").TextInfo;
			return textInfo.ToTitleCase(s.ToLowerInvariant()).Replace("_", string.Empty).Replace(".", string.Empty);
		}

		public static void GenerateEndpointFiles()
		{
			Console.WriteLine("Getting a listing of all the api endpoints from the elasticsearch-rest-api-spec repos");
			foreach(var listing in ApiListings.Select(l=>l.Replace("{version}", _version)))
				DownloadJsonDefinitions(listing);
		}

		private static void DownloadJsonDefinitions(string listingUrl)
		{
			string html = string.Empty;
			using (var client = new WebClient())
				html = client.DownloadString(listingUrl);

			FindJsonFilesOnListing(listingUrl, html);
		}

		private static void FindJsonFilesOnListing(string listingUrl, string html)
		{
			var dom = CQ.Create(html);

			WriteToEndpointsFolder("root.html", html);

			var endpoints = dom[".js-directory-link"]
				.Select(s => s.InnerText)
				.Where(s => !string.IsNullOrEmpty(s) && s.EndsWith(".json"))
				.ToList();

			endpoints.ForEach(s => WriteEndpointFile(listingUrl, s));
		}
		private static void WriteEndpointFile(string listingUrl, string s)
		{
			using (var client = new WebClient())
			{
				var rawFile = listingUrl.Replace("github.com", "raw.githubusercontent.com").Replace("tree/", "") + "/" + s;
				var fileName = rawFile.Split(new[] { '/' }).Last();
				Console.WriteLine("Downloading {0}", rawFile);
				var json = client.DownloadString(rawFile);
				WriteToEndpointsFolder(fileName, json);
			}
		}


		public static RestApiSpec GetRestApiSpec()
		{
			var spec = new RestApiSpec
			{
				Commit = CQ.Create(LocalUri("root.html"))[".sha:first"].Text(),
				Endpoints = Directory.GetFiles(_apiEndpointsFolder)
					.Where(f => f.EndsWith(".json"))
					.Select(f => CreateApiEndpoint(f))
					.ToDictionary(d => d.Key, d => d.Value)
			};

			return spec;
		}

		private static KeyValuePair<string, ApiEndpoint> CreateApiEndpoint(string jsonFile)
		{
			var json = File.ReadAllText(jsonFile);
			var endpoint = JsonConvert.DeserializeObject<Dictionary<string, ApiEndpoint>>(json).First();
			endpoint.Value.CsharpMethodName = CreateMethodName(endpoint.Key);
			return endpoint;
		}

		private static string LocalUri(string file)
		{
			var basePath = Path.Combine(Environment.CurrentDirectory, _apiEndpointsFolder + file);
			Console.WriteLine(basePath);
			var assemblyPath = Path.GetFullPath((new Uri(basePath)).LocalPath);
			var fileUri = new Uri(assemblyPath).AbsoluteUri;
			return fileUri;
		}

		private static readonly Dictionary<string, string> MethodNameOverrides =
			(from f in new DirectoryInfo(_nestFolder).GetFiles("*.cs", SearchOption.AllDirectories)
			 let contents = File.ReadAllText(f.FullName)
			 let c = Regex.Replace(contents, @"^.+\[DescriptorFor\(""([^ \r\n]+)""\)\].*$", "$1", RegexOptions.Singleline)
			 where !c.Contains(" ") //filter results that did not match
			 select new { Value = f.Name.Replace("Request", ""), Key = c })
			.DistinctBy(v => v.Key)
			.ToDictionary(k => k.Key, v => v.Value.Replace(".cs", ""));

		private static readonly Dictionary<string, string> KnownDescriptors =
			(from f in new DirectoryInfo(_nestFolder).GetFiles("*Request.cs", SearchOption.AllDirectories)
			 let contents = File.ReadAllText(f.FullName)
			 let c = Regex.Replace(contents, @"^.+class ([^ \r\n]+Descriptor(?:<[^>\r\n]+>)?[^ \r\n]*).*$", "$1", RegexOptions.Singleline)
			 select new { Key = Regex.Replace(c, "<.*$", ""), Value = Regex.Replace(c, @"^.*?(?:(\<.+>).*?)?$", "$1") })
			.DistinctBy(v => v.Key)
			.OrderBy(v=>v.Key)
			.ToDictionary(k => k.Key, v => v.Value);

		private static readonly Dictionary<string, string> KnownRequests =
			(from f in new DirectoryInfo(_nestFolder).GetFiles("*Request.cs", SearchOption.AllDirectories)
			 let contents = File.ReadAllText(f.FullName)
			 let c = Regex.Replace(contents, @"^.+interface ([^ \r\n]+Request(?:<[^>\r\n]+>)?[^ \r\n]*).*$", "$1", RegexOptions.Singleline)
			 where c.StartsWith("I") && c.Contains("Request")
			 select new { Key = Regex.Replace(c, "<.*$", ""), Value = Regex.Replace(c, @"^.*?(?:(\<.+>).*?)?$", "$1") })
			.DistinctBy(v => v.Key)
			.ToDictionary(k => k.Key, v => v.Value);


		//Patches a method name for the exceptions (IndicesStats needs better unique names for all the url endpoints)
		//or to get rid of double verbs in an method name i,e ClusterGetSettingsGet > ClusterGetSettings
		public static void PatchMethod(CsharpMethod method)
		{
			Func<string, bool> ms = (s) => method.FullName.StartsWith(s);
			Func<string, bool> pc = (s) => method.Path.Contains(s);

			if (ms("Indices") && !pc("{index}"))
				method.FullName = (method.FullName + "ForAll").Replace("AsyncForAll", "ForAllAsync");

			if (ms("Nodes") && !pc("{node_id}"))
				method.FullName = (method.FullName + "ForAll").Replace("AsyncForAll", "ForAllAsync");

			//remove duplicate occurance of the HTTP method name
			var m = method.HttpMethod.ToPascalCase();
			method.FullName =
				Regex.Replace(method.FullName, m, (a) => a.Index != method.FullName.IndexOf(m) ? "" : m);

			foreach (var param in GlobalQueryParameters.Parameters)
			{
				if (!method.Url.Params.ContainsKey(param.Key))
					method.Url.Params.Add(param.Key, param.Value);
			}

			string manualOverride;
			var key = method.QueryStringParamName.Replace("RequestParameters", "");
			if (MethodNameOverrides.TryGetValue(key, out manualOverride))
				method.QueryStringParamName = manualOverride + "RequestParameters";

			method.DescriptorType = method.QueryStringParamName.Replace("RequestParameters", "Descriptor");
			method.RequestType = method.QueryStringParamName.Replace("RequestParameters", "Request");
			string requestGeneric;
			if (KnownRequests.TryGetValue("I" + method.RequestType, out requestGeneric))
				method.RequestTypeGeneric = requestGeneric;
			else method.RequestTypeUnmapped = true;

			method.Allow404 = ApiEndpointsThatAllow404.Endpoints.Contains(method.DescriptorType.Replace("Descriptor", ""));

			string generic;
			if (KnownDescriptors.TryGetValue(method.DescriptorType, out generic))
				method.DescriptorTypeGeneric = generic;
			else method.Unmapped = true;

			try
			{
				IEnumerable<string> skipList = new List<string>();
				IDictionary<string, string> renameList = new Dictionary<string, string>();

				var typeName = "CodeGeneration.LowLevelClient.Overrides.Descriptors." + method.DescriptorType + "Overrides";
				var type = _assembly.GetType(typeName);
				if (type != null)
				{
					var overrides = Activator.CreateInstance(type) as IDescriptorOverrides;
					if (overrides != null)
					{
						skipList = overrides.SkipQueryStringParams ?? skipList;
						renameList = overrides.RenameQueryStringParams ?? renameList;
					}
				}

				var globalQueryStringRenames = new Dictionary<string, string>
				{
					{"_source", "source_enabled"},
					{"_source_include", "source_include"},
					{"_source_exclude", "source_exclude"},
					{"q", "query_on_query_string"},
				};

				foreach (var kv in globalQueryStringRenames)
					if (!renameList.ContainsKey(kv.Key))
						renameList[kv.Key] = kv.Value;

				var patchedParams = new Dictionary<string, ApiQueryParameters>();
				foreach (var kv in method.Url.Params)
				{
					if (kv.Value.OriginalQueryStringParamName.IsNullOrEmpty())
						kv.Value.OriginalQueryStringParamName = kv.Key;
					if (skipList.Contains(kv.Key))
						continue;

					string newName;
					if (!renameList.TryGetValue(kv.Key, out newName))
					{
						patchedParams.Add(kv.Key, kv.Value);
						continue;
					}
					
					patchedParams.Add(newName, kv.Value);

					//if (newName == "source_enabled")
					//{
					//	kv.Value.DeprecatedInFavorOf = "EnableSource";
					//	patchedParams.Add("enable_source", new ApiQueryParameters
					//	{
					//		Description = kv.Value.Description,
					//		Options = kv.Value.Options,
					//		Type = "boolean",
					//		OriginalQueryStringParamName = "_source"
					//	});
					//}
				}

				method.Url.Params = patchedParams;
			}
			// ReSharper disable once EmptyGeneralCatchClause
			catch
			{
			}

		}

		public static string CreateMethodName(string apiEnpointKey)
		{
			return PascalCase(apiEnpointKey);
		}

		public static void GenerateClientInterface(RestApiSpec model)
		{
			var targetFile = _esNetFolder + @"IElasticsearchClient.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"IElasticsearchClient.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}


		public static void GenerateRawDispatch(RestApiSpec model)
		{
			var targetFile = _nestFolder + @"_Generated/_LowLevelDispatch.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"_LowLevelDispatch.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}
		public static void GenerateRawClient(RestApiSpec model)
		{
			var targetFile = _esNetFolder + @"ElasticsearchClient.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"ElasticsearchClient.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}

		public static void GenerateDescriptors(RestApiSpec model)
		{
			var targetFile = _nestFolder + @"_Generated\_Descriptors.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"_Descriptors.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}

		public static void GenerateRequests(RestApiSpec model)
		{
			var targetFile = _nestFolder + @"_Generated\_Requests.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"_Requests.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}

		public static void GenerateRequestParameters(RestApiSpec model)
		{
			var targetFile = _esNetFolder + @"Domain\RequestParameters\RequestParameters.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"RequestParameters.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}

		public static void GenerateRequestParametersExtensions(RestApiSpec model)
		{
			var targetFile = _nestFolder + @"_Generated\_RequestParametersExtensions.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"_RequestParametersExtensions.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}
		public static void GenerateEnums(RestApiSpec model)
		{
			var targetFile = _esNetFolder + @"Domain\Enums.Generated.cs";
			var source = _razorMachine.Execute(File.ReadAllText(_viewFolder + @"Enums.Generated.cshtml"), model).ToString();
			File.WriteAllText(targetFile, source);
		}

		private static void WriteToEndpointsFolder(string filename, string contents)
		{
			if (!Directory.Exists(_apiEndpointsFolder))
				Directory.CreateDirectory(_apiEndpointsFolder);

			File.WriteAllText(_apiEndpointsFolder + filename, contents);
		}
	}
}