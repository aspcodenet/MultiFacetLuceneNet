using System.Collections.Generic;

namespace MultiFacetLucene.Configuration.MemoryOptimizer
{
    public interface IMemoryOptimizer
    {
        IEnumerable<FacetSearcher.FacetValues.FacetValueBitSet> SetAsLazyLoad(List<FacetSearcher.FacetValues> facetValuesList);
    }
}