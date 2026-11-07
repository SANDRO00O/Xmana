using System;

namespace Xmana.Core.Layout
{
    /// <summary>An inclusive range of grid rows.</summary>
    public struct RowRange
    {
        public RowRange(int first, int last)
        {
            First = first;
            Last = last;
        }

        public int First { get; }

        public int Last { get; }

        public bool IsEmpty => Last < First;

        public bool Contains(int row) => row >= First && row <= Last;

        public static RowRange Empty => new RowRange(0, -1);
    }

    /// <summary>
    /// Row arithmetic for the windowed poster grid (PROMPT.md section 57, memory optimisation): only the rows in and just around the
    /// viewport hold real cards; the rest are cheap placeholders of the same height. Pure functions so the rule is unit-tested.
    /// </summary>
    public static class GridWindow
    {
        /// <summary>Used until a real row has been measured.</summary>
        public const double DefaultRowHeight = 312;

        /// <summary>Rows kept realised above and below the viewport so fast D-pad scrolling never meets an empty row.</summary>
        public const int DefaultBufferRows = 3;

        public static int RowCount(int itemCount, int columns)
        {
            if (itemCount <= 0)
            {
                return 0;
            }

            var safeColumns = Math.Max(1, columns);
            return (itemCount + safeColumns - 1) / safeColumns;
        }

        public static int RowOf(int itemIndex, int columns) => Math.Max(0, itemIndex) / Math.Max(1, columns);

        /// <summary>The rows to keep realised for a viewport starting at <paramref name="offset"/> (pixels from the first row).</summary>
        public static RowRange Realized(int rowCount, double rowHeight, double offset, double viewport, int bufferRows = DefaultBufferRows)
        {
            if (rowCount <= 0)
            {
                return RowRange.Empty;
            }

            var height = rowHeight > 0 ? rowHeight : DefaultRowHeight;
            var top = Math.Max(0, offset);
            var visibleHeight = viewport > 0 ? viewport : height * 2;
            var buffer = Math.Max(0, bufferRows);

            var first = (int)Math.Floor(top / height) - buffer;
            var last = (int)Math.Ceiling((top + visibleHeight) / height) - 1 + buffer;
            first = Math.Max(0, Math.Min(first, rowCount - 1));
            last = Math.Max(first, Math.Min(last, rowCount - 1));
            return new RowRange(first, last);
        }

        /// <summary>The scroll offset that puts <paramref name="row"/> roughly in the middle of the viewport (never negative).</summary>
        public static double OffsetToShow(int row, double rowHeight, double viewport)
        {
            var height = rowHeight > 0 ? rowHeight : DefaultRowHeight;
            return Math.Max(0, row * height - Math.Max(0, viewport - height) / 2);
        }
    }
}
