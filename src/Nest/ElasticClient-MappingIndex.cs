﻿using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Elasticsearch.Net;

namespace Nest
{
	public partial class ElasticClient
	{
		/// <inheritdoc />
		public IIndexSettingsResponse GetIndexSettings(Func<GetIndexSettingsDescriptor, GetIndexSettingsDescriptor> selector)
		{
			return this.Dispatch<GetIndexSettingsDescriptor, GetIndexSettingsQueryString, IndexSettingsResponse>(
				selector,
				(p, d) => this.RawDispatch.IndicesGetSettingsDispatch<IndexSettingsResponse>(p)
			);
		}

		/// <inheritdoc />
		public Task<IIndexSettingsResponse> GetIndexSettingsAsync(
			Func<GetIndexSettingsDescriptor, GetIndexSettingsDescriptor> selector)
		{
			return this.DispatchAsync
				<GetIndexSettingsDescriptor, GetIndexSettingsQueryString, IndexSettingsResponse, IIndexSettingsResponse>(
					selector,
					(p, d) => this.RawDispatch.IndicesGetSettingsDispatchAsync<IndexSettingsResponse>(p)
				);
		}

		/// <inheritdoc />
		public IIndicesResponse DeleteIndex(Func<DeleteIndexDescriptor, DeleteIndexDescriptor> selector)
		{
			return this.Dispatch<DeleteIndexDescriptor, DeleteIndexQueryString, IndicesResponse>(
				selector,
				(p, d) => this.RawDispatch.IndicesDeleteDispatch<IndicesResponse>(p)
			);
		}

		/// <inheritdoc />
		public Task<IIndicesResponse> DeleteIndexAsync(Func<DeleteIndexDescriptor, DeleteIndexDescriptor> selector)
		{
			return this.DispatchAsync<DeleteIndexDescriptor, DeleteIndexQueryString, IndicesResponse, IIndicesResponse>(
				selector,
				(p, d) => this.RawDispatch.IndicesDeleteDispatchAsync<IndicesResponse>(p)
			);
		}
	}
}