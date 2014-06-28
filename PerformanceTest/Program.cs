using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using Lucene.Net.Store;
using MultiFacetLucene;
using Version = Lucene.Net.Util.Version;

namespace PerformanceTest
{
    class Program
    {
        private static FacetSearcher _target;

        static void Maina(string[] args)
        {
            _target = new FacetSearcher(SetupIndex());
            Warmup();

            var facetFieldInfos = new List<FacetFieldInfo>
                    {
                        new FacetFieldInfo{ FieldName = "color", MaxToFetchExcludingSelections   = 20},
                        new FacetFieldInfo{ FieldName = "type", MaxToFetchExcludingSelections   = 20},
                    };

            var stopwatchAll = new Stopwatch();
            stopwatchAll.Start();
            for (int i = 0; i < 100; i++)
            {
                var stopwatch = new Stopwatch();
                stopwatch.Start();
                var actual = _target.SearchWithFacets(new MatchAllDocsQuery(), 100, facetFieldInfos);
                stopwatch.Stop();
                var vs = stopwatch.ElapsedMilliseconds;
                Console.WriteLine("Took " + vs + " ms");
            }
            stopwatchAll.Stop();
            var vs2 = stopwatchAll.ElapsedMilliseconds;
            Console.WriteLine("Took " + vs2 + " ms - i.e " + vs2 / 100 + "ms/query");
        }

        public static void Warmup()
        {
            var facetFieldInfos = new List<FacetFieldInfo>
                    {
                        new FacetFieldInfo{ FieldName = "color", MaxToFetchExcludingSelections   = 20},
                        new FacetFieldInfo{ FieldName = "type", MaxToFetchExcludingSelections   = 20},
                    };

            //Warmup to prefetch facet bitset
            _target.SearchWithFacets(new TermQuery(new Term("Price", "5")), 100, facetFieldInfos);
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

                protected static IndexReader SetupIndexPhysicalTest()
                {
                    var directory = @"D:\Code\sites\SearchSite\SearchSiteWeb\App_Data\Index\Wordpress";
                    var index = FSDirectory.Open(directory);
                    return IndexReader.Open(index, true);
                }
                 static void Main(string[] args)
                {
                    long originalByteCount = GC.GetTotalMemory(true);
                    _target = new FacetSearcher(SetupIndexPhysicalTest());

                    var stopwatchAll = new Stopwatch();
                    stopwatchAll.Start();

                    var queryParser = new MultiFieldQueryParser(Version.LUCENE_30,
                        new[] { "title", "bodies" },
                        new StandardAnalyzer(Version.LUCENE_30)
                        );

                    var facetFieldInfos = new List<FacetFieldInfo>
                    {
                        new FacetFieldInfo{ FieldName = "userdisplayname",MaxToFetchExcludingSelections   = 20},
                        new FacetFieldInfo{ FieldName = "tag", MaxToFetchExcludingSelections   = 20},
                    };

                    for (int i = 0; i < 100; i++)
                    {
                        var stopwatch = new Stopwatch();
                        stopwatch.Start();
                        var query = queryParser.Parse("mysql");
                        var actual = _target.SearchWithFacets(query, 100, facetFieldInfos);
                        stopwatch.Stop();

                        foreach (var groupy in actual.Facets.Select(x => x.FacetFieldName).Distinct())
                        {
                            string @group = groupy;
                            System.IO.File.WriteAllLines(@"d:\\temp2\\" + group + ".txt",actual.Facets.Where(x=>x.FacetFieldName == group).Select(x=>x.Value + ":" + x.Count));
                        }
                        var vs = stopwatch.ElapsedMilliseconds;
                        Console.WriteLine("Took " + vs + " ms");
                    }
                    stopwatchAll.Stop();
                    var vs2 = stopwatchAll.ElapsedMilliseconds;
                    Console.WriteLine("Took " + vs2 + " ms - i.e " + vs2/100 + "ms/query");

                    long finalByteCount = GC.GetTotalMemory(true);

                    Console.WriteLine("START: " + originalByteCount + " END:" + finalByteCount);
                    Console.WriteLine("DIFF: " + (finalByteCount - originalByteCount));

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
