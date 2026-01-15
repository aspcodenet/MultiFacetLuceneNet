using System.Collections.Generic;
using Lucene.Net.Search;

namespace MultiFacetLucene
{
    public class FacetSearchResult
    {
        public List<FacetMatch> Facets { get; set; }
        public TopDocs Hits { get; set; }
    }
}