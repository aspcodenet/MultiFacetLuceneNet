using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiFacetLucene.MemoryOptimizer
{
    public class DefaultMemoryOptimizer : IMemoryOptimizer
    {
        private readonly int _keepPercent;
        private readonly int _optimizeIfTotalCountIsGreaterThan;

        public DefaultMemoryOptimizer(int keepPercent, int optimizeIfTotalCountIsGreaterThan)
        {
            _keepPercent = keepPercent;
            _optimizeIfTotalCountIsGreaterThan = optimizeIfTotalCountIsGreaterThan;
        }

        //Flag certain bitsets as lazyload (recalculate)
        //If total number of facet values is larger than...
        //have X percent removed
        public IEnumerable<FacetSearcher.FacetValues.FacetValueBitSet> SetAsLazyLoad(List<FacetSearcher.FacetValues> facetValuesList)
        {
            var totalCount = facetValuesList.Sum(a => a.FacetValueBitSetList.Count);
            if (totalCount < _optimizeIfTotalCountIsGreaterThan) yield break;
            foreach (var facetValues in facetValuesList)
            {
                var index = 0;
                var percent = Convert.ToInt32(totalCount * _keepPercent / 100.0);
                foreach (var value in facetValues.FacetValueBitSetList)
                {
                    if (index++ > percent)
                        yield return value;
                }
            }
        }
    }
}