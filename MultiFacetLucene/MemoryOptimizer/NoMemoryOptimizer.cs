using System.Collections.Generic;

namespace MultiFacetLucene.MemoryOptimizer
{
    public class NoMemoryOptimizer : IMemoryOptimizer
    {
        //Never flag any facetvalues as lazyload (recalculate)
        public IEnumerable<FacetSearcher.FacetValues.FacetValueBitSet> SetAsLazyLoad(List<FacetSearcher.FacetValues> facetValuesList)
        {
            yield break;
        }
    }
}