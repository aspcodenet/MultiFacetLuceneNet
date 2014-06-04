using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiFacetLucene;
using Lucene.Net.Util;

namespace MultiFacetLuceneNet.Tests
{
    [TestClass]
    public class FacetSearcherTest
    {
        private FacetSearcher _target;

        [TestInitialize]
        public void TestInitialize()
        {
            _target = new FacetSearcher(SetupIndex());
        }

        [TestMethod]
        public void MatchAllQueryShouldReturnCorrectFacetsAndDocuments()
        {
            var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, new List<SelectedFacet>(), new[] { "color", "type" });

            var colorFacets = actual.Facets.Where(x => x.FacetFieldName == "color").ToList();
            var typeFacets = actual.Facets.Where(x => x.FacetFieldName == "type").ToList();

            Assert.AreEqual(5, actual.Hits.TotalHits);

            Assert.AreEqual(3, colorFacets.Count);
            Assert.AreEqual(4, typeFacets.Count);

            Assert.AreEqual(3, colorFacets.Single(x => x.Value == "yellow").Count);
            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "white").Count);
            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "none").Count);

            Assert.AreEqual(2, typeFacets.Single(x => x.Value == "drink").Count);
            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "meat").Count);
            Assert.AreEqual(3, typeFacets.Single(x => x.Value == "food").Count);
            Assert.AreEqual(2, typeFacets.Single(x => x.Value == "fruit").Count);
        }

        [TestMethod]
        public void DrilldownSingleFacetSingleValueShouldReturnCorrectFacetsAndDocuments()
        {
            var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, new List<SelectedFacet>
            {
                new SelectedFacet{ FieldName = "color", SelectedValues = new List<string>{"yellow"}}
            }, new[] { "color", "type" });

            var colorFacets = actual.Facets.Where(x => x.FacetFieldName == "color").ToList();
            var typeFacets = actual.Facets.Where(x => x.FacetFieldName == "type").ToList();

            Assert.AreEqual(3, actual.Hits.TotalHits);

            Assert.AreEqual(3, colorFacets.Count);
            Assert.AreEqual(3, typeFacets.Count);

            Assert.AreEqual(3, colorFacets.Single(x => x.Value == "yellow").Count);
            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "white").Count);
            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "none").Count);

            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "meat").Count);
            Assert.AreEqual(3, typeFacets.Single(x => x.Value == "food").Count);
            Assert.AreEqual(2, typeFacets.Single(x => x.Value == "fruit").Count);
        }

        [TestMethod]
        public void DrilldownSingleFacetMultiValueShouldReturnCorrectFacetsAndDocuments()
        {
            var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, new List<SelectedFacet>
            {
                new SelectedFacet{ FieldName = "color", SelectedValues = new List<string>{"yellow", "none"}}
            }, new[] { "color", "type" });

            var colorFacets = actual.Facets.Where(x => x.FacetFieldName == "color").ToList();
            var typeFacets = actual.Facets.Where(x => x.FacetFieldName == "type").ToList();

            Assert.AreEqual(4, actual.Hits.TotalHits);

            Assert.AreEqual(3, colorFacets.Count);
            Assert.AreEqual(4, typeFacets.Count);

            Assert.AreEqual(3, colorFacets.Single(x => x.Value == "yellow").Count);
            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "white").Count);
            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "none").Count);

            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "meat").Count);
            Assert.AreEqual(3, typeFacets.Single(x => x.Value == "food").Count);
            Assert.AreEqual(2, typeFacets.Single(x => x.Value == "fruit").Count);
            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "drink").Count);
        }



        [TestMethod]
        public void MatchSpecifiedQueryShouldReturnCorrectFacetsAndDocuments()
        {
            var query = new QueryParser(Lucene.Net.Util.Version.LUCENE_30, string.Empty, new KeywordAnalyzer()).Parse("keywords:apa");

            var actual = _target.SearchWithFacets(query, 100, new List<SelectedFacet>(), new[] {"color", "type"});

            var colorFacets = actual.Facets.Where(x => x.FacetFieldName == "color").ToList();
            var typeFacets = actual.Facets.Where(x => x.FacetFieldName == "type").ToList();

            Assert.AreEqual(2, actual.Hits.TotalHits);
            Assert.AreEqual("Banana", _target.Doc(actual.Hits.ScoreDocs[0].Doc).GetField("title").StringValue);
            Assert.AreEqual("Water", _target.Doc(actual.Hits.ScoreDocs[1].Doc).GetField("title").StringValue);

            Assert.AreEqual(2, colorFacets.Count);
            Assert.AreEqual(3, typeFacets.Count);

            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "yellow").Count);
            Assert.AreEqual(1, colorFacets.Single(x => x.Value == "none").Count);

            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "drink").Count);
            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "food").Count);
            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "fruit").Count);
        }

        [TestMethod]
        public void DrilldownMultiFacetsShouldReturnCorrectFacetsAndDocuments()
        {
            var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, new List<SelectedFacet>
            {
                new SelectedFacet{ FieldName = "color", SelectedValues = new List<string>{"yellow"}},
                new SelectedFacet{ FieldName = "type", SelectedValues = new List<string>{"fruit"}}
            }, new[] { "color", "type" });

            var colorFacets = actual.Facets.Where(x => x.FacetFieldName == "color").ToList();
            var typeFacets = actual.Facets.Where(x => x.FacetFieldName == "type").ToList();

            Assert.AreEqual(2, actual.Hits.TotalHits);

            Assert.AreEqual(1, colorFacets.Count);
            Assert.AreEqual(3, typeFacets.Count);

            Assert.AreEqual(2, colorFacets.Single(x => x.Value == "yellow").Count); // only fruits

            Assert.AreEqual(1, typeFacets.Single(x => x.Value == "meat").Count); //only yellow
            Assert.AreEqual(2, typeFacets.Single(x => x.Value == "fruit").Count);//only yellow
            Assert.AreEqual(3, typeFacets.Single(x => x.Value == "food").Count);//only yellow
        }


        protected static IndexReader SetupIndex()
        {
            var directory = new RAMDirectory();
            var writer = new IndexWriter(directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), true,
                IndexWriter.MaxFieldLength.LIMITED);
            writer.AddDocument(new Document()
                .AddField("title", "Banana", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("color", "yellow", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "food", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "fruit", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("keywords", "apa hello whatever", Field.Store.YES, Field.Index.ANALYZED)
                .AddField("price", "10", Field.Store.YES, Field.Index.NOT_ANALYZED));
            writer.AddDocument(new Document()
                .AddField("title", "Apple", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("color", "yellow", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "food", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "fruit", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("price", "20", Field.Store.YES, Field.Index.NOT_ANALYZED));
            writer.AddDocument(new Document()
                .AddField("title", "Burger", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("color", "yellow", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "food", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "meat", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("price", "30", Field.Store.YES, Field.Index.NOT_ANALYZED));
            writer.AddDocument(new Document()
                .AddField("title", "Milk", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("color", "white", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "drink", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("price", "40", Field.Store.YES, Field.Index.NOT_ANALYZED));
            writer.AddDocument(new Document()
                .AddField("title", "Water", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("color", "none", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("type", "drink", Field.Store.YES, Field.Index.NOT_ANALYZED)
                .AddField("keywords", "apa hello cars", Field.Store.YES, Field.Index.ANALYZED)
                .AddField("price", "0", Field.Store.YES, Field.Index.NOT_ANALYZED));
            writer.Flush(true, true, true);
            writer.Optimize();
            writer.Commit();
            return  IndexReader.Open(directory, true);
            
        }
    }
}
