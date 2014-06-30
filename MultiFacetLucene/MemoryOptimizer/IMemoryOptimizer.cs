using System.Collections.Generic;

namespace MultiFacetLucene.MemoryOptimizer
{
    public interface IMemoryOptimizer
    {
        IEnumerable<FacetSearcher.FacetValues.FacetValueBitSet> SetAsLazyLoad(List<FacetSearcher.FacetValues> facetValuesList);
    }
}