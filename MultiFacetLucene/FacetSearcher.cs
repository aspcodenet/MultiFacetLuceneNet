using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Documents;
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

        public FacetSearcher(IndexReader reader, IndexReader[] subReaders, int[] docStarts) : base(reader, subReaders, docStarts)
        {
        }

        public FacetSearchResult SearchWithFacets(Query baseQueryWithoutFacetDrilldown, int topResults, List<SelectedFacet> selectedFacets, IEnumerable<string> facetAttributeFieldNames)
        {
            var hits = Search(CreateFacetedQuery(baseQueryWithoutFacetDrilldown, selectedFacets,null), topResults);

            var facets = GetAllFacetsValues(baseQueryWithoutFacetDrilldown, facetAttributeFieldNames, selectedFacets).Where(x => x.Count > 0).ToList();
            var va = facets.Where(x => x.Count > 1);
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

        private class FacetValues
        {
            public FacetValues()
            {
                FacetValueBitSetList = new List<FacetValueBitSet>();
            }
            public string Term { get; set; }

            public class FacetValueBitSet
            {
                public string Value { get; set; }
                public OpenBitSetDISI OpenBitSetDISI { get; set; }
                public Filter Filter { get; set; }
            }

            public List<FacetValueBitSet> FacetValueBitSetList { get; set; }
        }

        private FacetValues ReadBitSetsForValues(string facetAttributeFieldName)
        {
            var facetValues = new FacetValues();
            facetValues.Term = facetAttributeFieldName;

            facetValues.FacetValueBitSetList.AddRange( GetFacetValueTerms(facetAttributeFieldName).Select(fvt=>new FacetValues.FacetValueBitSet
            {
                Value = fvt.Term,
                Filter = fvt.Filter,
                OpenBitSetDISI = new OpenBitSetDISI(fvt.Filter.GetDocIdSet(IndexReader).Iterator(), IndexReader.MaxDoc)
            }) );

            return facetValues;
        }

        private class FacetValueTermFilter
        {
            public string Term { get; set; }
            public Filter Filter { get; set; }
        }

        private IEnumerable<FacetValueTermFilter> GetFacetValueTerms(string facetAttributeFieldName)
        {
            var termReader = IndexReader.Terms(new Term(facetAttributeFieldName, String.Empty));
            do
            {
                if (termReader.Term.Field != facetAttributeFieldName)
                    yield break;

                var facetQuery = new TermQuery(termReader.Term.CreateTerm(termReader.Term.Text));
                var facetQueryFilter = new CachingWrapperFilter(new QueryWrapperFilter(facetQuery));
                yield return new FacetValueTermFilter { Term = termReader.Term.Text, Filter = facetQueryFilter };
            }
            while (termReader.Next());

        }

        private IEnumerable<FacetMatch> GetAllFacetsValues(Query baseQueryWithoutFacetDrilldown, IEnumerable<string> facetAttributeFieldNames, List<SelectedFacet> selectedFacets)
        {
            return facetAttributeFieldNames.SelectMany(facetAttributeFieldName => FindMatchesInQuery(baseQueryWithoutFacetDrilldown, selectedFacets, facetAttributeFieldName));
        }

        private IEnumerable<FacetMatch> FindMatchesInQuery(Query baseQueryWithoutFacetDrilldown, IEnumerable<SelectedFacet> selectedFacets, string facetAttributeFieldName)
        {
            var matches = GetOrCreateFacetBitSet(facetAttributeFieldName).FacetValueBitSetList.Select(value =>
            {
                var queryFilter = new QueryWrapperFilter(CreateFacetedQuery(baseQueryWithoutFacetDrilldown, selectedFacets, facetAttributeFieldName));
                var bitsQuery = new OpenBitSetDISI(queryFilter.GetDocIdSet(IndexReader).Iterator(), IndexReader.MaxDoc);
                bitsQuery.And(value.OpenBitSetDISI);
                var count = bitsQuery.Cardinality();

                return new FacetMatch() { Count = count, Value = value.Value, FacetFieldName = facetAttributeFieldName };
            }).ToList();

            return matches;
        }


        protected Query CreateFacetedQuery(Query baseQueryWithoutFacetDrilldown, IEnumerable<SelectedFacet> selectedFacets, string facetAttributeFieldName)
        {
            var facetsToAdd = selectedFacets.Where(x => x.FieldName != facetAttributeFieldName).ToList();
            if (!facetsToAdd.Any()) return baseQueryWithoutFacetDrilldown;
            var booleanQuery = new BooleanQuery { { baseQueryWithoutFacetDrilldown, Occur.MUST } };
            foreach (var selectedFacet in facetsToAdd)
            {
                if (selectedFacet.SelectedValues.Count == 1)
                    booleanQuery.Add(new TermQuery(new Term(selectedFacet.FieldName, selectedFacet.SelectedValues[0])), Occur.MUST);
                else
                {
                    var valuesQuery = new BooleanQuery();
                    foreach (var value in selectedFacet.SelectedValues)
                    {
                        valuesQuery.Add(new TermQuery(new Term(selectedFacet.FieldName, value)), Occur.SHOULD);
                    }
                    booleanQuery.Add(valuesQuery, Occur.MUST);
                }
            }
            return booleanQuery;
        }


    }
}
