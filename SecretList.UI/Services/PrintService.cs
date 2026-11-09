// SecretList
// Copyright (c) 2026 edwest19
//
// AI Disclaimer: This code was generated with the assistance of
// Claude (Anthropic AI), under the direction and review of edwest19.

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Printing;
using SecretList.Models;
using Windows.Graphics.Printing;

namespace SecretList.Services;

// Handles printing the full record collection onto A4 sheets, two
// records per sheet (one per half), so a printed stack can be folded
// horizontally into an A5 booklet. Plain black-on-white text,
// printer-friendly, duplex requested by default.
public class PrintService
{
    // AI-generated (Claude): removed the temporary print-debug.log logging
    // (Log(), LogPath and every Log call). It wrote each record's category and
    // nickname to a file in LocalState. Printing itself is unchanged.

    private PrintDocument? _printDocument;
    private PrintManager? _printManager;
    private DispatcherQueue? _dispatcherQueue;
    private List<UIElement> _pages = new();

    private List<EntityDefinition> _schema = new();
    private List<EntityRecord> _records = new();

    // A4 at 96 DPI (8.27in x 11.69in), portrait. Two records print per
    // sheet - one in the top half, one in the bottom half - so folding
    // the sheet in half horizontally yields an A5-sized page on each side.
    // The 12-tags-per-category cap enforced in SchemaFileStore is what
    // makes it safe to assume a single record's fields always fit within
    // one half - there's no per-record pagination/overflow handling here.
    private const double PageWidth = 794;
    private const double PageHeight = 1123;
    private const double Margin = 32;

    // A thin strip reserved for the fold-line marker between the two
    // halves. Each half gets exactly (PageHeight - FoldGap) / 2.
    private const double FoldGap = 16;
    private const double HalfHeight = (PageHeight - FoldGap) / 2;

    // Call this once, when the app starts, passing the window handle
    // (hwnd) so the print system knows which window this belongs to.
    public void Initialize(nint hwnd)
    {
        _printManager = PrintManagerInterop.GetForWindow(hwnd);
        _printManager.PrintTaskRequested += OnPrintTaskRequested;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        // Create the PrintDocument once, here, on the UI thread. The
        // CreatePrintTask callback below can run on a worker thread with
        // no DispatcherQueue, and constructing a new PrintDocument (a
        // XAML DependencyObject) there throws a marshaling exception
        // that gets swallowed silently - so we build it once up front
        // and just reuse its DocumentSource on every print request.
        _printDocument = new PrintDocument();
        _printDocument.Paginate += OnPaginate;
        _printDocument.GetPreviewPage += OnGetPreviewPage;
        _printDocument.AddPages += OnAddPages;
    }

    // Call this to actually show the Windows print dialog.
    public async Task ShowPrintUIAsync(nint hwnd, List<EntityDefinition> schema, List<EntityRecord> records)
    {
        _schema = schema;
        _records = records;

        await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
    }

    // Fires when the user actually confirms printing from the dialog.
    // Sets up the PrintDocument and builds every page in advance.
    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        var printTask = args.Request.CreatePrintTask("SecretList", sourceRequested =>
        {
            // sourceRequested runs on a worker thread with no DispatcherQueue -
            // touching _printDocument (a UI-thread-affine object) directly here
            // throws RPC_E_WRONG_THREAD. Take a deferral and marshal the actual
            // work back onto the UI thread's DispatcherQueue.
            var deferral = sourceRequested.GetDeferral();
            _dispatcherQueue!.TryEnqueue(() =>
            {
                try
                {
                    sourceRequested.SetSource(_printDocument!.DocumentSource);
                }
                catch (Exception)
                {
                    // AI-generated (Claude): was logged to print-debug.log; still not shown to the user.
                }
                finally
                {
                    deferral.Complete();
                }
            });
        });

        // Request A4 paper and duplex, flipped on the short edge - the
        // binding axis that matches a sheet meant to be folded
        // horizontally in half. Whether the printer/driver actually
        // honors these is out of our control; the print dialog still
        // lets the user override paper size/duplex themselves.
        try
        {
            printTask.Options.MediaSize = PrintMediaSize.IsoA4;
            printTask.Options.Duplex = PrintDuplex.TwoSidedShortEdge;
        }
        catch (Exception)
        {
            // AI-generated (Claude): was logged to print-debug.log. Not every driver
            // exposes these options; the print dialog still lets the user choose.
        }
    }

    private void OnPaginate(object sender, PaginateEventArgs e)
    {
        try
        {
            _pages = BuildAllPages();
        }
        catch (Exception)
        {
            // AI-generated (Claude): was logged to print-debug.log.
            _pages = new List<UIElement>();
        }

        _printDocument!.SetPreviewPageCount(_pages.Count, PreviewPageCountType.Final);
    }

    private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
    {
        _printDocument!.SetPreviewPage(e.PageNumber, _pages[e.PageNumber - 1]);
    }

    private void OnAddPages(object sender, AddPagesEventArgs e)
    {
        foreach (var page in _pages)
        {
            _printDocument!.AddPage(page);
        }
        _printDocument!.AddPagesComplete();
    }

    // AI-generated (Claude): which records are printed, in what order, and which of
    // their fields now comes from SecretList.Core's PrintContent (tested there):
    // schema categories in schema order, then records whose category isn't in the
    // schema, with values the schema doesn't list after the schema's fields. This only
    // lays them out. A sheet is simply "the next two records" and freely crosses
    // category boundaries. The last sheet gets a blank bottom half if the total
    // record count is odd.
    private List<UIElement> BuildAllPages()
    {
        var flat = PrintContent.Build(_schema, _records);

        var pages = new List<UIElement>();
        for (int i = 0; i < flat.Count; i += 2)
        {
            var top = flat[i];
            PrintedRecord? bottom = i + 1 < flat.Count ? flat[i + 1] : null;

            pages.Add(BuildA4Sheet(top, bottom));
        }

        return pages;
    }

    // One A4 sheet: top half, a thin fold-line marker, bottom half.
    // Bottom is optional - the very last sheet may only have a top half
    // if the total record count is odd.
    private UIElement BuildA4Sheet(PrintedRecord top, PrintedRecord? bottom) // AI-generated (Claude): takes PrintContent's records
    {
        var pageGrid = new Grid
        {
            Width = PageWidth,
            Height = PageHeight,
            Background = new SolidColorBrush(Microsoft.UI.Colors.White)
        };

        var layout = new Grid();
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HalfHeight) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(FoldGap) });
        layout.RowDefinitions.Add(new RowDefinition { Height = new GridLength(HalfHeight) });

        var topHalf = BuildRecordHalf(top);
        Grid.SetRow(topHalf, 0);
        layout.Children.Add(topHalf);

        var foldLine = new Microsoft.UI.Xaml.Shapes.Rectangle
        {
            Height = 1,
            Fill = new SolidColorBrush(Microsoft.UI.Colors.Gray),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(Margin, 0, Margin, 0)
        };
        Grid.SetRow(foldLine, 1);
        layout.Children.Add(foldLine);

        if (bottom != null)
        {
            var bottomHalf = BuildRecordHalf(bottom);
            Grid.SetRow(bottomHalf, 2);
            layout.Children.Add(bottomHalf);
        }

        pageGrid.Children.Add(layout);
        return pageGrid;
    }

    // One record's content, within a single A5-equivalent half of the A4
    // sheet: the category (gray), the nickname (bold), then each field that
    // has a value. A category has at most 12 fields (SchemaFileStore), so a
    // record with short values fits; values outside the schema or very long
    // values can run past the half (README, Known limitations).
    private FrameworkElement BuildRecordHalf(PrintedRecord record) // AI-generated (Claude): takes a PrintedRecord
    {
        var halfGrid = new Grid { Height = HalfHeight };

        var content = new StackPanel
        {
            Margin = new Thickness(Margin, Margin / 2, Margin, Margin / 2),
            Spacing = 4
        };

        content.Children.Add(new TextBlock
        {
            Text = record.Category,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Gray)
        });

        content.Children.Add(new TextBlock
        {
            Text = record.NickName,
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 18,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
            Margin = new Thickness(0, 0, 0, 4)
        });

        foreach (var field in record.Fields) // AI-generated (Claude): PrintContent already left out empty values
        {
            content.Children.Add(new TextBlock
            {
                Text = $"{field.Name}: {field.Value}",
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 11,
                Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black),
                Margin = new Thickness(10, 0, 0, 0),
                TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
                Width = PageWidth - (Margin * 2) - 10
            });
        }

        halfGrid.Children.Add(content);
        return halfGrid;
    }
}
