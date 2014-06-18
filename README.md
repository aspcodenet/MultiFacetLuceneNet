MultiFacetLuceneNet
===================

Lucene.NET with simplified (and correct!) facet behaviour

What is this? Well, to me correct facet behaviour is for each facet "group" to exclude selections concerning itself from the calculation of facets. 

Stolen from the documentation of SOLR - here is what I mean:

To implement a multi-select facet for doctype, a GUI may want to still display the other doctype values and their associated counts, as if the doctype:pdf constraint had not yet been applied. Example:


=== Document Type ===
  [ ] Word (42)
  [x] PDF  (96)
  [ ] Excel(11)
  [ ] HTML (63)


In short this library gives you a derived searcher class, FacetSearcher

Sample usage explains it best:
=============================

        [TestMethod]
        public void DrilldownMultiFacetsShouldReturnCorrectFacetsAndDocuments()
        {
            var _target = new FacetSearcher(SetupIndex());
        
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


Some more background is available at http://www.stefanholmberg.com/post/2014/06/05/Lucenenet-and-multiple-facets

DEMO SITE
=========

http://aviationquestions.aircraftdata.net/search/aviation

Here you get right into the search for  "aviation". See to the right, User and Tags are facet values. Now, when clicking  one of the users, search result is indeed filtered  - but as you can see you can still select more users. I.e possible to multi value facetting!

NUGET
=====
https://www.nuget.org/packages/MultiFacetLucene.Net
