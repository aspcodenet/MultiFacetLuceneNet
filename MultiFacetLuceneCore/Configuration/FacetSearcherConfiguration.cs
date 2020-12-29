using MultiFacetLucene.Configuration.MemoryOptimizer;

namespace MultiFacetLucene.Configuration
{
    public class FacetSearcherConfiguration
    {
        public FacetSearcherConfiguration()
        {
            MinimumCountInTotalDatasetForFacet = 1;
            MemoryOptimizer = null;
        }
        public static FacetSearcherConfiguration Default()
        {
            return new FacetSearcherConfiguration { MinimumCountInTotalDatasetForFacet  = 1, MemoryOptimizer = null};
        }
        public int MinimumCountInTotalDatasetForFacet { get; set; }

        public IMemoryOptimizer MemoryOptimizer { get; set; }
    }
}