using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Store;
using MultiFacetLucene;

namespace PerformanceTest
{
    class Program
    {
        private static FacetSearcher _target;

        static void Main(string[] args)
        {
            _target = new FacetSearcher(SetupIndex());
            Warmup();

            var stopwatchAll = new Stopwatch();
            stopwatchAll.Start();
            for (int i = 0; i < 100; i++)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, new List<SelectedFacet>(), new[] { "color", "type" });
                stopwatch.Stop();
                var vs = stopwatch.ElapsedMilliseconds;
                Console.WriteLine("Took " + vs + " ms");
            }
            stopwatchAll.Stop();
            var vs2 = stopwatchAll.ElapsedMilliseconds;
            Console.WriteLine("Took " + vs2 + " ms - i.e " + vs2/100 + "ms/query");

        }

        public static void Warmup()
        {
            //Warmup to prefetch facet bitset
            _target.SearchWithFacets(new TermQuery(new Term("Price", "5")), 100, new List<SelectedFacet>(), new[] { "color", "type" });
        }



        protected static IndexReader SetupIndex()
        {
            var directory = new RAMDirectory();
            var writer = new IndexWriter(directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), true,
                IndexWriter.MaxFieldLength.LIMITED);
            for (int i = 0; i < 50000; i++)
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

        private static Random _rnd = new Random(Guid.NewGuid().GetHashCode());
        private static string GetRandom(int i, int i1)
        {
            return _rnd.Next(i, i1).ToString("00000");
        }

    }
}
