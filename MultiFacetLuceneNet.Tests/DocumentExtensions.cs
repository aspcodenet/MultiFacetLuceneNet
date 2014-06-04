using System;
using Lucene.Net.Documents;

namespace MultiFacetLuceneNet.Tests
{
    public static class DocumentExtensions
    {
        public static Document AddField(this Document document, string name, string value, Field.Store store, Field.Index index)
        {
            if (String.IsNullOrEmpty(value))
                return document;

            document.Add(new Field(name, value, store, index));
            return document;
        }
    }
}