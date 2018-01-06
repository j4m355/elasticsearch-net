﻿using System;
using System.Collections.Generic;
using System.Linq;
using Nest.Resolvers.Converters;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Nest
{
	[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
	[JsonConverter(typeof(ReadAsTypeConverter<HasChildQueryDescriptor<object>>))]
	public interface IHasChildQuery : IQuery
	{
		[JsonProperty("type")]
		TypeNameMarker Type { get; set; }

		[JsonProperty("score_type")]
		[JsonConverter(typeof(StringEnumConverter))]
		ChildScoreType? ScoreType { get; set; }

		[JsonProperty("min_children")]
		int? MinChildren { get; set; }

		[JsonProperty("max_children")]
		int? MaxChildren { get; set; }

		[JsonProperty("query")]
		[JsonConverter(typeof(CompositeJsonConverter<ReadAsTypeConverter<QueryDescriptor<object>>, CustomJsonConverter>))]
		IQueryContainer Query { get; set; }

		[JsonProperty("inner_hits")]
		[JsonConverter(typeof(ReadAsTypeConverter<InnerHits>))]
		IInnerHits InnerHits { get; set; }

		[JsonProperty("boost")]
		double? Boost { get; set; }
	}

	public class HasChildQuery : PlainQuery, IHasChildQuery
	{
		protected override void WrapInContainer(IQueryContainer container)
		{
			container.HasChild = this;
		}

		public string Name { get; set; }
		bool IQuery.IsConditionless { get { return false; } }
		public TypeNameMarker Type { get; set; }
		public ChildScoreType? ScoreType { get; set; }
		public int? MinChildren { get; set; }
		public int? MaxChildren { get; set; }
		public IQueryContainer Query { get; set; }
		public IInnerHits InnerHits { get; set; }
		public double? Boost { get; set; }
	}

	public class HasChildQueryDescriptor<T> : IHasChildQuery where T : class
	{
		private IHasChildQuery Self { get { return this; } }

		TypeNameMarker IHasChildQuery.Type { get; set; }

		ChildScoreType? IHasChildQuery.ScoreType { get; set; }

		int? IHasChildQuery.MinChildren { get; set; }

		int? IHasChildQuery.MaxChildren { get; set; }

		IQueryContainer IHasChildQuery.Query { get; set; }

		IInnerHits IHasChildQuery.InnerHits { get; set; }

		double? IHasChildQuery.Boost { get; set; }

		string IQuery.Name { get; set; }

		bool IQuery.IsConditionless
		{
			get
			{
				return Self.Query == null || Self.Query.IsConditionless;
			}
		}

		public HasChildQueryDescriptor()
		{
			Self.Type = TypeNameMarker.Create<T>();
		}

		public HasChildQueryDescriptor<T> Name(string name)
		{
			Self.Name = name;
			return this;
		}

		public HasChildQueryDescriptor<T> Query(Func<QueryDescriptor<T>, QueryContainer> querySelector)
		{
			var q = new QueryDescriptor<T>();
			Self.Query = querySelector(q);
			return this;
		}

		public HasChildQueryDescriptor<T> Type(string type)
		{
			Self.Type = type;
			return this;
		}

		public HasChildQueryDescriptor<T> Score(ChildScoreType? scoreType)
		{
			Self.ScoreType = scoreType;
			return this;
		}

		public HasChildQueryDescriptor<T> MinChildren(int minChildren)
		{
			Self.MinChildren = minChildren;
			return this;
		}

		public HasChildQueryDescriptor<T> MaxChildren(int maxChildren)
		{
			Self.MaxChildren = maxChildren;
			return this;
		}

		public HasChildQueryDescriptor<T> InnerHits()
		{
			Self.InnerHits = new InnerHits();
			return this;
		}

		public HasChildQueryDescriptor<T> InnerHits(Func<InnerHitsDescriptor<T>, IInnerHits> innerHitsSelector)
		{
			if (innerHitsSelector == null) return this;
			Self.InnerHits = innerHitsSelector(new InnerHitsDescriptor<T>());
			return this;
		}
	}
}
