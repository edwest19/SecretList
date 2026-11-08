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
using Windows.Storage;

namespace SecretList.Services;

// Handles printing the full record collection onto A4 sheets, two
// records per sheet (one per half), so a printed stack can be folded
// horizontally into an A5 booklet. Plain black-on-white text,
// printer-friendly, duplex requested by default.
public class PrintService
{
    // TEMP DEBUG: Console.WriteLine is invisible when the app is launched via
    // AUMID/package identity (as `dotnet run` does for this project), so log
    // to a plain file in the app's own sandboxed AppData instead - same folder
    // records.txt lives in. Cleared fresh each time Initialize() runs.
    // Remove once printing is confirmed working long-term.
    private static readonly string LogPath =
        Path.Combine(ApplicationData.Current.LocalFolder.Path, "print-debug.log");

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch { /* logging must never crash the app */ }
    }

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
        try { File.Delete(LogPath); } catch { } // truncate previous run's log
        Log($"Initialize called, hwnd={hwnd}");
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
        Log("PrintTaskRequested handler attached, PrintDocument created");
    }

    // Call this to actually show the Windows print dialog.
    public async Task ShowPrintUIAsync(nint hwnd, List<EntityDefinition> schema, List<EntityRecord> records)
    {
        _schema = schema;
        _records = records;
        Log($"ShowPrintUIAsync called, hwnd={hwnd}, schema.Count={schema.Count}, records.Count={records.Count}");
        foreach (var r in records)
            Log($"  record: Category='{r.Category}', NickName='{r.NickName}'");

        bool shown = await PrintManagerInterop.ShowPrintUIForWindowAsync(hwnd);
        Log($"ShowPrintUIForWindowAsync returned {shown}");
    }

    // Fires when the user actually confirms printing from the dialog.
    // Sets up the PrintDocument and builds every page in advance.
    private void OnPrintTaskRequested(PrintManager sender, PrintTaskRequestedEventArgs args)
    {
        Log("OnPrintTaskRequested fired");

        var printTask = args.Request.CreatePrintTask("SecretList", sourceRequested =>
        {
            Log("CreatePrintTask callback fired");

            // sourceRequested runs on a worker thread with no DispatcherQueue -
            // touching _printDocument (a UI-thread-affine object) directly here
            // throws RPC_E_WRONG_THREAD. Take a deferral and marshal the actual
            // work back onto the UI thread's DispatcherQueue.
            var deferral = sourceRequested.GetDeferral();
            bool enqueued = _dispatcherQueue!.TryEnqueue(() =>
            {
                try
                {
                    sourceRequested.SetSource(_printDocument!.DocumentSource);
                    Log("SetSource called");
                }
                catch (Exception ex)
                {
                    Log("EXCEPTION setting source: " + ex);
                }
                finally
                {
                    deferral.Complete();
                }
            });
            Log($"TryEnqueue returned {enqueued}");
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
            Log("Requested MediaSize=IsoA4, Duplex=TwoSidedShortEdge");
        }
        catch (Exception ex)
        {
            Log("Could not set MediaSize/Duplex options: " + ex);
        }
    }

    private void OnPaginate(object sender, PaginateEventArgs e)
    {
        Log("OnPaginate fired");
        try
        {
            _pages = BuildAllPages();
            Log($"Built {_pages.Count} pages");
        }
        catch (Exception ex)
        {
            Log("EXCEPTION in BuildAllPages: " + ex);
            _pages = new List<UIElement>();
        }

        _printDocument!.SetPreviewPageCount(_pages.Count, PreviewPageCountType.Final);
        Log($"SetPreviewPageCount({_pages.Count}) called");
    }

    private void OnGetPreviewPage(object sender, GetPreviewPageEventArgs e)
    {
        Log($"OnGetPreviewPage fired for page {e.PageNumber}");
        _printDocument!.SetPreviewPage(e.PageNumber, _pages[e.PageNumber - 1]);
    }

    private void OnAddPages(object sender, AddPagesEventArgs e)
    {
        Log($"OnAddPages fired, adding {_pages.Count} pages");
        foreach (var page in _pages)
        {
            _printDocument!.AddPage(page);
        }
        _printDocument!.AddPagesComplete();
    }

    // Builds one A4 page per two records, in schema order and then
    // per-category list order - so category grouping is preserved in the
    // reading sequence, but a sheet is simply "the next two records" and
    // freely crosses category boundaries. The last sheet gets a blank
    // bottom half if the total record count is odd.
    private List<UIElement> BuildAllPages()
    {
        var flat = new List<(EntityDefinition Def, EntityRecord Record)>();
        foreach (var entityDef in _schema)
        {
            var typeRecords = _records.Where(r => r.Category == entityDef.Category).ToList();
            foreach (var record in typeRecords)
                flat.Add((entityDef, record));
        }

        Log($"BuildAllPages: {flat.Count} total records across {_schema.Count} categories");

        var pages = new List<UIElement>();
        for (int i = 0; i < flat.Count; i += 2)
        {
            var top = flat[i];
            (EntityDefinition Def, EntityRecord Record)? bottom =
                i + 1 < flat.Count ? flat[i + 1] : null;

            pages.Add(BuildA4Sheet(top, bottom));
        }

        return pages;
    }

    // One A4 sheet: top half, a thin fold-line marker, bottom half.
    // Bottom is optional - the very last sheet may only have a top half
    // if the total record count is odd.
    private UIElement BuildA4Sheet(
        (EntityDefinition Def, EntityRecord Record) top,
        (EntityDefinition Def, EntityRecord Record)? bottom)
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

        var topHalf = BuildRecordHalf(top.Def, top.Record);
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

        if (bottom.HasValue)
        {
            var bottomHalf = BuildRecordHalf(bottom.Value.Def, bottom.Value.Record);
            Grid.SetRow(bottomHalf, 2);
            layout.Children.Add(bottomHalf);
        }

        pageGrid.Children.Add(layout);
        return pageGrid;
    }

    // One record's content, sized to fit within a single A5-equivalent
    // half of the A4 sheet. Safe to assume it always fits because
    // SchemaFileStore rejects any schema with more than 12 tags in a
    // category.
    // Before:
// After:
    private FrameworkElement BuildRecordHalf(EntityDefinition entityDef, EntityRecord record){
        var halfGrid = new Grid { Height = HalfHeight };

        var content = new StackPanel
        {
            Margin = new Thickness(Margin, Margin / 2, Margin, Margin / 2),
            Spacing = 4
        };

        content.Children.Add(new TextBlock
        {
            Text = entityDef.Category,
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

        foreach (var tag in entityDef.Tags.OrderBy(t => t.Order))
        {
            string value = record.Values.TryGetValue(tag.Name, out var v) ? v : string.Empty;
            if (string.IsNullOrWhiteSpace(value))
                continue;

            content.Children.Add(new TextBlock
            {
                Text = $"{tag.Name}: {value}",
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