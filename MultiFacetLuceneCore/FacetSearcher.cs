using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Lucene.Net.Util;
using MultiFacetLucene.Configuration;

namespace MultiFacetLucene
{
    public class FacetSearcher : IndexSearcher
    {
        private readonly ConcurrentDictionary<string, FacetValues> _facetBitSetDictionary = new ConcurrentDictionary<string, FacetValues>();

        public FacetSearcher(IndexReaderContext context, FacetSearcherConfiguration facetSearcherConfiguration = null)
            : base(context)
        {
            Initialize(facetSearcherConfiguration);
        }

        public FacetSearcher(IndexReader r, TaskScheduler executor, FacetSearcherConfiguration facetSearcherConfiguration = null)
            : base(r, executor)
        {
            Initialize(facetSearcherConfiguration);
        }

        public FacetSearcher(IndexReader r, FacetSearcherConfiguration facetSearcherConfiguration = null)
            : base(r)
        {
            Initialize(facetSearcherConfiguration);
        }

        public FacetSearcher(IndexReaderContext context, TaskScheduler executor, FacetSearcherConfiguration facetSearcherConfiguration = null)
            : base(context, executor)
        {
            Initialize(facetSearcherConfiguration);
        }

        public FacetSearcherConfiguration FacetSearcherConfiguration { get; protected set; }

        private void Initialize(FacetSearcherConfiguration facetSearcherConfiguration)
        {
            FacetSearcherConfiguration = facetSearcherConfiguration ?? FacetSearcherConfiguration.Default();
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

            if (FacetSearcherConfiguration.MemoryOptimizer == null) return facetValues;
            foreach (var facetValue in FacetSearcherConfiguration.MemoryOptimizer.SetAsLazyLoad(_facetBitSetDictionary.Values.ToList()))
                facetValue.Bitset = null;

            return facetValues;
        }

        private IEnumerable<FacetValues.FacetValueBitSet> GetFacetValueTerms(string facetAttributeFieldName)
        {
            var termReader = MultiFields.GetTerms(IndexReader, facetAttributeFieldName).GetEnumerator();
            
                do
                {
                    if (termReader.Term != null && termReader.Term.Bytes.Length > 0)
                    {
                    var termString = System.Text.Encoding.UTF8.GetString(termReader.Term.Bytes, 0, termReader.Term.Length).TrimEnd('\0');
                        var bitset = CalculateOpenBitSetDisi(facetAttributeFieldName, termReader.Term);
                        var cnt = bitset.Cardinality();
                        if (cnt >= FacetSearcherConfiguration.MinimumCountInTotalDatasetForFacet)
                            yield return new FacetValues.FacetValueBitSet { Value = termString, Bitset = bitset, Count = cnt };
                        else
                        {
                            bitset = null;
                        }
                    }
                } while (termReader.MoveNext());
        }

        protected OpenBitSetDISI CalculateOpenBitSetDisi(string facetAttributeFieldName, BytesRef value)
        {
            //var facetQuery = new TermQuery(new Term(facetAttributeFieldName, value));
            //var facetQueryFilter = new QueryWrapperFilter(facetQuery);
           // var liveDocs = MultiFields.GetLiveDocs(IndexReader);
            var termDocsEnum = MultiFields.GetTermDocsEnum(IndexReader, null, facetAttributeFieldName, value);
            return new OpenBitSetDISI(termDocsEnum, IndexReader.MaxDoc);
        }

        protected OpenBitSetDISI CalculateOpenBitSetDisiForFilteredData(CachingWrapperFilter filter, string facetAttributeFieldName, BytesRef value)
        {
           // var liveDocs = MultiFields.GetLiveDocs(IndexReader);
            var termDocsEnum = MultiFields.GetTermDocsEnum(IndexReader, null, facetAttributeFieldName, value);
            return new OpenBitSetDISI(termDocsEnum, IndexReader.MaxDoc);
        }

        private IEnumerable<FacetMatch> GetAllFacetsValues(Query baseQueryWithoutFacetDrilldown,
            IList<FacetFieldInfo> facetFieldInfos)
        {
            return
                facetFieldInfos.SelectMany(
                    facetFieldInfo =>
                        FindMatchesInQuery(baseQueryWithoutFacetDrilldown, facetFieldInfos, facetFieldInfo));
        }
        private DocIdSet GetDocIdSet(CachingWrapperFilter cachingWrapperFilter)
        {
            FixedBitSet idSet = new FixedBitSet(IndexReader.MaxDoc);
            foreach (AtomicReaderContext ctx in IndexReader.Context.Leaves)
            {
                AtomicReader atomicReader = ctx.AtomicReader;
                var iterator = cachingWrapperFilter.GetDocIdSet(atomicReader.AtomicContext, atomicReader.LiveDocs)?.GetIterator();
                if (iterator == null)
                {
                    //  return EMPTY_DOCIDSET;
                }
                else
                {
                    idSet.Or(iterator);
                }
            }

            return idSet as DocIdSet;
        }

        private IEnumerable<FacetMatch> FindMatchesInQuery(Query baseQueryWithoutFacetDrilldown, IList<FacetFieldInfo> allFacetFieldInfos, FacetFieldInfo facetFieldInfoToCalculateFor)
        {
            var calculations = 0;
            var queryFilter = new CachingWrapperFilter(new QueryWrapperFilter(CreateFacetedQuery(baseQueryWithoutFacetDrilldown, allFacetFieldInfos, facetFieldInfoToCalculateFor.FieldName)));
       //     var docIdSet = GetDocIdSet(queryFilter);
            var calculatedFacetCounts = new ResultCollection(facetFieldInfoToCalculateFor);
            foreach (var facetValueBitSet in GetOrCreateFacetBitSet(facetFieldInfoToCalculateFor.FieldName).FacetValueBitSetList)
            {
                var isSelected = calculatedFacetCounts.IsSelected(facetValueBitSet.Value);

                if (!isSelected && facetValueBitSet.Count < calculatedFacetCounts.MinCountForNonSelected) //Impossible to get a better result
                {
                    if (calculatedFacetCounts.HaveEnoughResults)
                        break;
                }

                var bitset = facetValueBitSet.Bitset ?? CalculateOpenBitSetDisi(facetFieldInfoToCalculateFor.FieldName, new BytesRef(facetValueBitSet.Value));
                var count = GetFacetCountFromMultipleIndices(queryFilter, bitset);
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

        private long GetFacetCountFromMultipleIndices(CachingWrapperFilter filter, OpenBitSetDISI facetValueBitSet)
        {
            long count = 0;
            foreach (AtomicReaderContext ctx in IndexReader.Leaves)
            {
                AtomicReader atomicReader = ctx.AtomicReader;
                // TODO: Poznamka pro priste, az budu resit ze se spatne hledaji pocty facetu, zda se ze to souvisi s NULL hodnotama, mozna vyfiltrovat not NULL?
                var iterator = filter.GetDocIdSet(atomicReader.AtomicContext, atomicReader.LiveDocs)?.GetIterator();
                if (iterator != null)
                {
                    OpenBitSetDISI baseQueryWithoutFacetDrilldownCopy = new OpenBitSetDISI(iterator, atomicReader.MaxDoc);
                    baseQueryWithoutFacetDrilldownCopy.And(facetValueBitSet);
                    count += baseQueryWithoutFacetDrilldownCopy.Cardinality();
                }
            }
            
            return count;
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