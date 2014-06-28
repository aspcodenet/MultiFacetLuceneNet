using System.Collections.Generic;

namespace MultiFacetLucene
{
    public class FacetFieldInfo
    {
        public FacetFieldInfo()
        {
            Selections = new List<string>();
            MaxToFetchExcludingSelections = 20;
        }
        public string FieldName { get; set; }
        public List<string> Selections { get; set; }
        public int MaxToFetchExcludingSelections { get; set; }
    }
}