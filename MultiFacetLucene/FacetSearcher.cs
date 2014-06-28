using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;

namespace MultiFacetLucene
{
    public class FacetSearcher : IndexSearcher
    {
        private readonly ConcurrentDictionary<string, FacetValues> _facetBitSetDictionary = new ConcurrentDictionary<string, FacetValues>();

        public FacetSearcher(Directory path)
            : base(path)
        {
        }

        public FacetSearcher(Directory path, bool readOnly) : base(path, readOnly)
        {
        }

        public FacetSearcher(IndexReader r) : base(r)
        {
        }

        public FacetSearcher(IndexReader reader, IndexReader[] subReaders, int[] docStarts)
            : base(reader, subReaders, docStarts)
        {
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
            var facetValues = new FacetValues();
            facetValues.Term = facetAttributeFieldName;

            facetValues.FacetValueBitSetList.AddRange(
                GetFacetValueTerms(facetAttributeFieldName).Select(fvt =>
                {
                    var value = new FacetValues.FacetValueBitSet
                    {
                        Value = fvt.Term,
                        Bitset = fvt.Bitset,
                        Count = fvt.Bitset.Cardinality(),
                    };
                    return value;
                }).OrderByDescending(x => x.Count));

            return facetValues;
        }

        private IEnumerable<FacetValueTermBitset> GetFacetValueTerms(string facetAttributeFieldName)
        {
            using(var termReader = IndexReader.Terms(new Term(facetAttributeFieldName, String.Empty)))
            {
                do
                {
                    if (termReader.Term.Field != facetAttributeFieldName)
                        yield break;

                    var facetQuery = new TermQuery(termReader.Term.CreateTerm(termReader.Term.Text));
                    var facetQueryFilter = new QueryWrapperFilter(facetQuery);
                    var bitset = new OpenBitSetDISI(facetQueryFilter.GetDocIdSet(IndexReader).Iterator(), IndexReader.MaxDoc);

                    yield return new FacetValueTermBitset { Term = termReader.Term.Text, Bitset = bitset };
                } while (termReader.Next());
            }
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

                baseQueryWithoutFacetDrilldownCopy.And(facetValueBitSet.Bitset);
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

        private class FacetValueTermBitset
        {
            public string Term { get; set; }
            public OpenBitSetDISI Bitset { get; set; }
        }

        private class FacetValues
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