using System.Collections.Generic;

namespace MultiFacetLucene
{
    public class SelectedFacet
    {
        public SelectedFacet()
        {
            SelectedValues = new List<string>();
        }

        public string FieldName { get; set; }
        public List<string> SelectedValues { get; set; }
    }
}