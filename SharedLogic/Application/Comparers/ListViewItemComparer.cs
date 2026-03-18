using System;
using System.Collections;
using organizadorCapitulos.Core.Enums;

namespace organizadorCapitulos.Application.Comparers
{
    public class ListViewItemComparer : IComparer
    {
        private readonly int _column;
        private readonly SortOrder _sortOrder;

        public ListViewItemComparer(int column, SortOrder sortOrder)
        {
            _column = column;
            _sortOrder = sortOrder;
        }

        public int Compare(object? x, object? y)
        {
            string sx = x?.ToString() ?? string.Empty;
            string sy = y?.ToString() ?? string.Empty;
            int returnVal = string.Compare(sx, sy, StringComparison.OrdinalIgnoreCase);
            return _sortOrder == SortOrder.Ascending ? returnVal : -returnVal;
        }
    }
}
