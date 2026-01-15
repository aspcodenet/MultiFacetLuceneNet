using System;
using System.Collections.Generic;
using System.Linq;

namespace MultiFacetLucene
{
    internal class ResultCollection
    {
        private readonly FacetFieldInfo _facetFieldInfoToCalculateFor;
        private int _uncalculatedSelectedCount;
        public long MinCountForNonSelected { get; protected set; }

        public ResultCollection(FacetFieldInfo facetFieldInfoToCalculateFor)
        {
            MinCountForNonSelected = 0;
            _facetFieldInfoToCalculateFor = facetFieldInfoToCalculateFor;
            _uncalculatedSelectedCount = facetFieldInfoToCalculateFor.Selections.Count;
            NonSelectedMatches = new List<FacetMatch>();
            SelectedMatches = new List<FacetMatch>();
        }

        public bool HaveEnoughResults
        {
            get { return _uncalculatedSelectedCount == 0 && NonSelectedMatches.Count >= _facetFieldInfoToCalculateFor.MaxToFetchExcludingSelections; }
        }

        public bool IsSelected(string facetValue)
        {
            return _uncalculatedSelectedCount > 0 && _facetFieldInfoToCalculateFor.Selections.Contains(facetValue);
        }



        public void AddToNonSelected(FacetMatch match)
        {
            if (NonSelectedMatches.Count >= _facetFieldInfoToCalculateFor.MaxToFetchExcludingSelections)
            {
                if (match.Count < MinCountForNonSelected)
                    return;
                if (match.Count > MinCountForNonSelected)
                {
                    //Remove tail if possible
                    while (true)
                    {
                        var allWithMinCount = NonSelectedMatches.Where(x => x.Count == MinCountForNonSelected).ToList();
                        if (allWithMinCount.Count == 0)
                            break;
                        var countWhenAddingThisAndRemovingMin = NonSelectedMatches.Count - allWithMinCount.Count + 1;
                        if (countWhenAddingThisAndRemovingMin >= _facetFieldInfoToCalculateFor.MaxToFetchExcludingSelections)
                        {
                            allWithMinCount.ForEach(x => NonSelectedMatches.Remove(x));
                            MinCountForNonSelected = NonSelectedMatches.Min(x => x.Count);
                        }
                        else
                        {
                            break;
                        }
                    }

                }
            }

            MinCountForNonSelected = MinCountForNonSelected == 0 ? match.Count : Math.Min(MinCountForNonSelected, match.Count);

            NonSelectedMatches.Add(match);
        }

        public void AddToSelected(FacetMatch match)
        {
            SelectedMatches.Add(match);
            _uncalculatedSelectedCount--;
        }

        protected List<FacetMatch> NonSelectedMatches { get; set; }
        private List<FacetMatch> SelectedMatches { get; set; }

        public IEnumerable<FacetMatch> GetList()
        {
            return SelectedMatches.Union(NonSelectedMatches).OrderByDescending(x => x.Count);
        }
    }
}