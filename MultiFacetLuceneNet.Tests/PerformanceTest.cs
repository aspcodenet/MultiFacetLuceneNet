using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MultiFacetLucene;
using Version = Lucene.Net.Util.Version;

namespace MultiFacetLuceneNet.Tests
{
    [Ignore]
    [TestClass]
    public class PerformanceTest
    {
        private static FacetSearcher _target;
        private static readonly Random _rnd = new Random(Guid.NewGuid().GetHashCode());

        public void Warmup()
        {
            //Warmup to prefetch facet bitset
            var facetFieldInfos = new List<FacetFieldInfo>
            {
                new FacetFieldInfo{ FieldName = "color"},
                new FacetFieldInfo{ FieldName = "type"},
            };
            _target.SearchWithFacets(new TermQuery(new Term("Price", "5")), 100, facetFieldInfos);
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _target = new FacetSearcher(SetupIndex());
            Warmup();
        }

        [TestMethod]
        public void MatchAllDocsAndCalculateFacetsPerformanceTest()
        {
            Warmup();
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var facetFieldInfos = new List<FacetFieldInfo>
            {
                new FacetFieldInfo{ FieldName = "color"},
                new FacetFieldInfo{ FieldName = "type"},
            };
            var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, facetFieldInfos);


            stopwatch.Stop();
            var vs = stopwatch.ElapsedMilliseconds;
            Trace.WriteLine("Took " + vs + " ms");
        }

        [TestMethod]
        public void MatchAllDocsPerformanceTest()
        {
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, new List<FacetFieldInfo>());


            stopwatch.Stop();
            var vs = stopwatch.ElapsedMilliseconds;
            Trace.WriteLine("Took " + vs + " ms");
        }


        protected static IndexReader SetupIndex()
        {
            var directory = new RAMDirectory();
            var writer = new IndexWriter(directory, new StandardAnalyzer(Version.LUCENE_30), true,
                IndexWriter.MaxFieldLength.LIMITED);
            for (var i = 0; i < 50000; i++)
                writer.AddDocument(new Document()
                    .AddField("title", Guid.NewGuid().ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED)
                    .AddField("color", GenerateColor(), Field.Store.YES, Field.Index.NOT_ANALYZED)
                    .AddField("type", GenerateFood(), Field.Store.YES, Field.Index.NOT_ANALYZED)
                    .AddField("type", GenerateFruit(), Field.Store.YES, Field.Index.NOT_ANALYZED)
                    .AddField("price", "10", Field.Store.YES, Field.Index.NOT_ANALYZED));
            writer.Flush(true, true, true);
            writer.Optimize();
            writer.Commit();
            return IndexReader.Open(directory, true);
        }


        private static string GenerateFruit()
        {
            return "fruit" + GetRandom(1000, 2000); // 1000 different values
        }

        private static string GenerateFood()
        {
            return "food" + GetRandom(1000, 1100); // 100 different values
        }

        private static string GenerateColor()
        {
            return "color" + GetRandom(1000, 1100); // 30 different values
        }

        private static string GetRandom(int i, int i1)
        {
            return _rnd.Next(i, i1).ToString("00000");
        }
    }
}