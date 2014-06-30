using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MultiFacetLucene.MemoryOptimizer;

namespace MultiFacetLucene
{
    public class FacetSearcher : IndexSearcher
    {
        private readonly ConcurrentDictionary<string, FacetValues> _facetBitSetDictionary = new ConcurrentDictionary<string, FacetValues>();
        public IMemoryOptimizer MemoryOptimizer { get; protected set; }

        public FacetSearcher(Directory path, IMemoryOptimizer memoryOptimizer = null)
            : base(path)
        {
            MemoryOptimizer = memoryOptimizer;
        }

        public FacetSearcher(Directory path, bool readOnly, IMemoryOptimizer memoryOptimizer = null)
            : base(path, readOnly)
        {
            MemoryOptimizer = memoryOptimizer;
        }

        public FacetSearcher(IndexReader r, IMemoryOptimizer memoryOptimizer = null)
            : base(r)
        {
            MemoryOptimizer = memoryOptimizer;
        }

        public FacetSearcher(IndexReader reader, IndexReader[] subReaders, int[] docStarts, IMemoryOptimizer memoryOptimizer = null)
            : base(reader, subReaders, docStarts)
        {
            MemoryOptimizer = memoryOptimizer;
        }



        public FacetSearchResult SearchWithFacets(Query baseQueryWithoutFacetDrilldown, int topResults, IList<FacetFieldInfo> facetFieldInfos)
        {
            var hits = Search(CreateFacetedQuery(baseQueryWithoutFacetDrilldown, facetFieldInfos, null), topResults);

            var facets = GetAllFacetsValues(baseQueryWithoutFacetDrilldown, facetFieldInfos)
                .Where(x => x.Count > 0)
                .ToList();
            return new FacetSearchResult()
            {
                Facets = facets,
                Hits = hits
            };
        }


        private FacetValues GetOrCreateFacetBitSet(string facetAttributeFieldName)
        {
            return _facetBitSetDictionary.GetOrAdd(facetAttributeFieldName, ReadBitSetsForValues);
        }


        private FacetValues ReadBitSetsForValues(string facetAttributeFieldName)
        {
            var facetValues = new FacetValues {Term = facetAttributeFieldName};

            facetValues.FacetValueBitSetList.AddRange(GetFacetValueTerms(facetAttributeFieldName).OrderByDescending(x => x.Count));

            if (MemoryOptimizer == null) return facetValues;
            foreach (var facetValue in MemoryOptimizer.SetAsLazyLoad(_facetBitSetDictionary.Values.ToList()))
                facetValue.Bitset = null;

            return facetValues;
        }

        private IEnumerable<FacetValues.FacetValueBitSet> GetFacetValueTerms(string facetAttributeFieldName)
        {
            using (var termReader = IndexReader.Terms(new Term(facetAttributeFieldName, String.Empty)))
            {
                do
                {
                    if (termReader.Term.Field != facetAttributeFieldName)
                        yield break;

                    var bitset = CalculateOpenBitSetDisi(facetAttributeFieldName, termReader.Term.Text);
                    yield return new FacetValues.FacetValueBitSet {Value = termReader.Term.Text, Bitset = bitset, Count = bitset.Cardinality()};
                } while (termReader.Next());
            }
        }

        protected OpenBitSetDISI CalculateOpenBitSetDisi(string facetAttributeFieldName, string value)
        {
            var facetQuery = new TermQuery(new Term(facetAttributeFieldName, value));
            var facetQueryFilter = new QueryWrapperFilter(facetQuery);
            return new OpenBitSetDISI(facetQueryFilter.GetDocIdSet(IndexReader).Iterator(), IndexReader.MaxDoc);
        }

        private IEnumerable<FacetMatch> GetAllFacetsValues(Query baseQueryWithoutFacetDrilldown,
            IList<FacetFieldInfo> facetFieldInfos)
        {
            return
                facetFieldInfos.SelectMany(
                    facetFieldInfo =>
                        FindMatchesInQuery(baseQueryWithoutFacetDrilldown, facetFieldInfos, facetFieldInfo));
        }

        private IEnumerable<FacetMatch> FindMatchesInQuery(Query baseQueryWithoutFacetDrilldown, IList<FacetFieldInfo> allFacetFieldInfos, FacetFieldInfo facetFieldInfoToCalculateFor)
        {
            var calculations = 0;

            var queryFilter = new CachingWrapperFilter(new QueryWrapperFilter(CreateFacetedQuery(baseQueryWithoutFacetDrilldown, allFacetFieldInfos, facetFieldInfoToCalculateFor.FieldName)));
            var bitsQueryWithoutFacetDrilldown = new OpenBitSetDISI(queryFilter.GetDocIdSet(IndexReader).Iterator(), IndexReader.MaxDoc);
            var baseQueryWithoutFacetDrilldownCopy = new OpenBitSetDISI(bitsQueryWithoutFacetDrilldown.Bits.Length)
            {
                Bits = new long[bitsQueryWithoutFacetDrilldown.Bits.Length]
            };

            var calculatedFacetCounts = new ResultCollection(facetFieldInfoToCalculateFor);
            foreach (var facetValueBitSet in GetOrCreateFacetBitSet(facetFieldInfoToCalculateFor.FieldName).FacetValueBitSetList)
            {
                var isSelected = calculatedFacetCounts.IsSelected(facetValueBitSet.Value);

                if (!isSelected && facetValueBitSet.Count < calculatedFacetCounts.MinCountForNonSelected) //Impossible to get a better result
                {
                    if (calculatedFacetCounts.HaveEnoughResults)
                        break;
                }

                bitsQueryWithoutFacetDrilldown.Bits.CopyTo(baseQueryWithoutFacetDrilldownCopy.Bits, 0);
                baseQueryWithoutFacetDrilldownCopy.NumWords = bitsQueryWithoutFacetDrilldown.NumWords;

                var bitset = facetValueBitSet.Bitset ?? CalculateOpenBitSetDisi(facetFieldInfoToCalculateFor.FieldName, facetValueBitSet.Value);
                baseQueryWithoutFacetDrilldownCopy.And(bitset);
                var count = baseQueryWithoutFacetDrilldownCopy.Cardinality();
                if (count == 0)
                    continue;
                var match = new FacetMatch
                {
                    Count = count,
                    Value = facetValueBitSet.Value,
                    FacetFieldName = facetFieldInfoToCalculateFor.FieldName
                };

                calculations++;
                if (isSelected)
                    calculatedFacetCounts.AddToSelected(match);
                else
                    calculatedFacetCounts.AddToNonSelected(match);
            }

            return calculatedFacetCounts.GetList();
        }


        protected Query CreateFacetedQuery(Query baseQueryWithoutFacetDrilldown, IList<FacetFieldInfo> facetFieldInfos, string facetAttributeFieldName)
        {
            var facetsToAdd = facetFieldInfos.Where(x => x.FieldName != facetAttributeFieldName && x.Selections.Any()).ToList();
            if (!facetsToAdd.Any()) return baseQueryWithoutFacetDrilldown;
            var booleanQuery = new BooleanQuery {{baseQueryWithoutFacetDrilldown, Occur.MUST}};
            foreach (var facetFieldInfo in facetsToAdd)
            {
                if (facetFieldInfo.Selections.Count == 1)
                    booleanQuery.Add(new TermQuery(new Term(facetFieldInfo.FieldName, facetFieldInfo.Selections[0])), Occur.MUST);
                else
                {
                    var valuesQuery = new BooleanQuery();
                    foreach (var value in facetFieldInfo.Selections)
                    {
                        valuesQuery.Add(new TermQuery(new Term(facetFieldInfo.FieldName, value)), Occur.SHOULD);
                    }
                    booleanQuery.Add(valuesQuery, Occur.MUST);
                }
            }
            return booleanQuery;
        }

        public class FacetValues
        {
            public FacetValues()
            {
                FacetValueBitSetList = new List<FacetValueBitSet>();
            }

            public string Term { get; set; }

            public List<FacetValueBitSet> FacetValueBitSetList { get; set; }

            public class FacetValueBitSet
            {
                public string Value { get; set; }
                public OpenBitSetDISI Bitset { get; set; }
                public long Count { get; set; }
            }
        }
    }
}