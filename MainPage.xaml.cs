// SecretList
// Copyright (c) 2026 edwest19
//
// AI Disclaimer: This code was generated with the assistance of
// Claude (Anthropic AI), under the direction and review of edwest19.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Input;
using SecretList.Models;
using SecretList.Services;
using SecretList.ViewModels;
using Windows.Storage;

namespace SecretList;

public sealed partial class MainPage : Page
{
    private readonly RecordFileStore _store = new();
    private readonly SchemaFileStore _schemaStore = new();
    private List<EntityDefinition> _schema = new();
    private List<EntityRecord> _records = new();

    private int _currentTypeIndex = 0;
    private int _currentRecordIndex = 0;
    private bool _isLocked = true;

    // True while the Find overlay is open - stops normal record
    // navigation keys from firing while you're searching.
    private bool _searchOverlayVisible = false;

    // Tracks a record created via "Add New" that hasn't been locked/saved
    // yet - Cancel removes it outright rather than reverting values.
    private EntityRecord? _pendingNewRecord;

    // Snapshot of an EXISTING record's values taken when unlocking to edit
    // it, so Cancel can restore them instead of just discarding textbox
    // changes (the textboxes aren't the source of truth until Save runs).
    private string? _editSnapshotNickName;
    private Dictionary<string, string>? _editSnapshotValues;

    // Prevents CategoryComboBox_SelectionChanged from firing when
    // ShowCurrentRecord sets the selection programmatically.
    private bool _suppressCategoryEvent = false;

    // The entity Type we're currently viewing, e.g. "LEGAL".
    // Note: on EntityRecord, this same concept is called "Category".
    private string CurrentType => _schema[_currentTypeIndex].Category;

    private List<EntityRecord> CurrentTypeRecords =>
        _records.Where(r => r.Category == CurrentType).ToList();

    private string DataFilePath =>
        Path.Combine(ApplicationData.Current.LocalFolder.Path, "records.txt");

    private string SchemaFilePath =>
        Path.Combine(ApplicationData.Current.LocalFolder.Path, "schema.md");

    private readonly PrintService _printService = new();

    public MainPage()
    {
        this.InitializeComponent();
        this.Loaded += MainPage_Loaded;

        _schema = _schemaStore.Load(SchemaFilePath, out bool isSchemaValid);
        CategoryComboBox.ItemsSource = _schema.Select(e => e.Category).ToList();
        UpdateImportButtonStatus(isSchemaValid);

        LoadRecords();
        ShowCurrentRecord();
    }

private void MainPage_Loaded(object sender, RoutedEventArgs e)
{
    if (App.MainWindowInstance.Content is UIElement rootElement)
    {
        rootElement.AddHandler(
            UIElement.KeyDownEvent,
            new Microsoft.UI.Xaml.Input.KeyEventHandler(Page_KeyDown),
            true);
    }

    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
    _printService.Initialize(hwnd);
}

    private void LoadRecords()
    {
        _records = _store.Load(DataFilePath);
    }

    private void ShowCurrentRecord()
    {
        var typeRecords = CurrentTypeRecords;
        var entityDef = _schema.FirstOrDefault(e => e.Category == CurrentType);

        LockButton.Content = _isLocked ? "Unlock" : "Lock & Save";

        // Type nav depends only on _currentTypeIndex, so it's the same in
        // both branches below. Record nav differs per-branch (no records
        // means nothing to page through). All nav is disabled while
        // unlocked/editing - see the comment on the toolbar rows in
        // MainPage.xaml.
        bool typeNavEnabled = _isLocked;
        TypeFirstButton.IsEnabled = typeNavEnabled && _currentTypeIndex > 0;
        TypePrevButton.IsEnabled = typeNavEnabled && _currentTypeIndex > 0;
        TypeNextButton.IsEnabled = typeNavEnabled && _currentTypeIndex < _schema.Count - 1;
        TypeLastButton.IsEnabled = typeNavEnabled && _currentTypeIndex < _schema.Count - 1;

        // Importing a new schema mid-edit could swap out the field layout
        // the user is actively typing into - same guard as the nav buttons.
        // Export carries no such risk (it never touches active app state),
        // so it stays enabled regardless of lock state.
        ImportSchemaMenuItem.IsEnabled = _isLocked;

        if (typeRecords.Count == 0)
        {
            HeaderText.Text = $"{CurrentType}  ·  No records yet";
            NicknameTextBox.Text = string.Empty;
            NicknameTextBox.IsReadOnly = _isLocked;

            RecordFirstButton.IsEnabled = false;
            RecordPrevButton.IsEnabled = false;
            RecordNextButton.IsEnabled = false;
            RecordLastButton.IsEnabled = false;

            _suppressCategoryEvent = true;
            CategoryComboBox.SelectedItem = CurrentType;
            _suppressCategoryEvent = false;
            CategoryPickerPanel.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;

            var emptyRows = new List<FieldRow>();
            if (entityDef != null)
            {
                foreach (var tag in entityDef.Tags.OrderBy(t => t.Order))
                {
                    emptyRows.Add(new FieldRow
                    {
                        TagName = tag.Name + ":",
                        Value = string.Empty,
                        IsReadOnly = _isLocked
                    });
                }
            }
            FieldsControl.ItemsSource = emptyRows;
            return;
        }

        if (_currentRecordIndex >= typeRecords.Count)
            _currentRecordIndex = typeRecords.Count - 1;

        bool recordNavEnabled = _isLocked;
        RecordFirstButton.IsEnabled = recordNavEnabled && _currentRecordIndex > 0;
        RecordPrevButton.IsEnabled = recordNavEnabled && _currentRecordIndex > 0;
        RecordNextButton.IsEnabled = recordNavEnabled && _currentRecordIndex < typeRecords.Count - 1;
        RecordLastButton.IsEnabled = recordNavEnabled && _currentRecordIndex < typeRecords.Count - 1;

        var record = typeRecords[_currentRecordIndex];

        HeaderText.Text = $"{record.Category}  ·  Record {_currentRecordIndex + 1} of {typeRecords.Count}";
        NicknameTextBox.Text = record.NickName;
        NicknameTextBox.IsReadOnly = _isLocked;

        _suppressCategoryEvent = true;
        CategoryComboBox.SelectedItem = record.Category;
        _suppressCategoryEvent = false;
        CategoryPickerPanel.Visibility = _isLocked ? Visibility.Collapsed : Visibility.Visible;

        var rows = new List<FieldRow>();
        if (entityDef != null)
        {
            foreach (var tag in entityDef.Tags.OrderBy(t => t.Order))
            {
                string value = record.Values.TryGetValue(tag.Name, out var v) ? v : string.Empty;
                rows.Add(new FieldRow
                {
                    TagName = tag.Name + ":",
                    Value = value,
                    IsReadOnly = _isLocked
                });
            }
        }

        FieldsControl.ItemsSource = rows;
    }

    private void ToggleLock()
    {
        if (!_isLocked)
        {
            SaveCurrentRecordEdits();
            _pendingNewRecord = null;
            _editSnapshotNickName = null;
            _editSnapshotValues = null;
        }
        else
        {
            // About to unlock an existing record for editing - snapshot its
            // current values so Cancel can restore them if the edit is abandoned.
            var typeRecords = CurrentTypeRecords;
            if (typeRecords.Count > 0 && _currentRecordIndex < typeRecords.Count)
            {
                var record = typeRecords[_currentRecordIndex];
                _editSnapshotNickName = record.NickName;
                _editSnapshotValues = new Dictionary<string, string>(record.Values);
            }
        }

        _isLocked = !_isLocked;
        ShowCurrentRecord();
    }

    private void SaveCurrentRecordEdits()
    {
        var typeRecords = CurrentTypeRecords;
        if (typeRecords.Count == 0)
            return;

        var record = typeRecords[_currentRecordIndex];
        record.NickName = NicknameTextBox.Text;

        if (FieldsControl.ItemsSource is List<FieldRow> rows)
        {
            foreach (var row in rows)
            {
                string tagName = row.TagName.TrimEnd(':');
                record.Values[tagName] = row.Value;
            }
        }

        _store.Save(DataFilePath, _records, _schema);
    }

    private void AddButton_Click(object sender, RoutedEventArgs e)
    {
        var newRecord = new EntityRecord
        {
            Category = CurrentType,
            NickName = "New" + CurrentType,
            Values = new Dictionary<string, string>()
        };

        _records.Add(newRecord);
        _pendingNewRecord = newRecord;
        _editSnapshotNickName = null;
        _editSnapshotValues = null;
        _currentRecordIndex = CurrentTypeRecords.Count - 1;

        _isLocked = false;
        ShowCurrentRecord();

        NicknameTextBox.Focus(FocusState.Programmatic);
        NicknameTextBox.SelectAll();
    }

    // Backs out of whatever's currently unlocked without saving:
    // - a brand-new record from "Add New" gets removed outright.
    // - edits to an existing record get reverted to their last-saved values.
    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        CancelEdit();
    }

    private void CancelEdit()
    {
        if (_isLocked)
            return; // nothing to cancel

        if (_pendingNewRecord != null)
        {
            _records.Remove(_pendingNewRecord);
            _pendingNewRecord = null;

            var remaining = CurrentTypeRecords;
            _currentRecordIndex = remaining.Count > 0
                ? Math.Min(_currentRecordIndex, remaining.Count - 1)
                : 0;
        }
        else if (_editSnapshotValues != null)
        {
            var typeRecords = CurrentTypeRecords;
            if (_currentRecordIndex < typeRecords.Count)
            {
                var record = typeRecords[_currentRecordIndex];
                record.NickName = _editSnapshotNickName ?? record.NickName;
                record.Values = new Dictionary<string, string>(_editSnapshotValues);
            }
        }

        _editSnapshotNickName = null;
        _editSnapshotValues = null;
        _isLocked = true;
        ShowCurrentRecord();
    }

    // Lets you reassign a record's category while adding or editing it.
    // Follows the record to its new type tab so it stays visible/selected.
    private void CategoryComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressCategoryEvent || _isLocked)
            return;

        if (CategoryComboBox.SelectedItem is not string newCategory)
            return;

        var typeRecords = CurrentTypeRecords;
        if (typeRecords.Count == 0 || _currentRecordIndex >= typeRecords.Count)
            return;

        var record = typeRecords[_currentRecordIndex];
        if (record.Category == newCategory)
            return;

        record.Category = newCategory;

        int newTypeIndex = _schema.FindIndex(t => t.Category == newCategory);
        if (newTypeIndex >= 0)
        {
            _currentTypeIndex = newTypeIndex;
            var newTypeRecords = CurrentTypeRecords;
            int idx = newTypeRecords.IndexOf(record);
            _currentRecordIndex = idx >= 0 ? idx : 0;
        }

        ShowCurrentRecord();
    }

    // Green/red status color for the "..." menu button: green means
    // schema.md is currently in place and parsed successfully (whether
    // that's a custom import or the freshly-seeded default); red means
    // it's missing/broken and the app has fallen back to the built-in
    // default categories.
    private void UpdateImportButtonStatus(bool isValid)
    {
        MoreButton.Background = (Microsoft.UI.Xaml.Media.Brush)Resources[isValid ? "ImportValidBrush" : "ImportInvalidBrush"];
        ToolTipService.SetToolTip(MoreButton, isValid
            ? "schema.md loaded successfully - these are your active categories."
            : "schema.md is missing or couldn't be read - using the built-in default categories instead.");
    }

    // Lets you pick a schema file from anywhere on disk (Desktop, Documents,
    // a USB drive, etc.) instead of hand-editing the copy in AppData. Only
    // commits it as the app's real schema.md if it actually parses.
    private async void ImportSchemaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".txt");
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

        var file = await picker.PickSingleFileAsync();
        if (file == null)
            return; // user cancelled the picker

        bool success = _schemaStore.TryImport(file.Path, SchemaFilePath, out var newSchema);

        if (success)
        {
            _schema = newSchema;
            CategoryComboBox.ItemsSource = _schema.Select(c => c.Category).ToList();
            _currentTypeIndex = 0;
            _currentRecordIndex = 0;
            UpdateImportButtonStatus(true);
            ShowCurrentRecord();
        }
        else
        {
            // Nothing about the active schema changed - leave the button's
            // color exactly as it was, just explain why nothing happened.
            var dialog = new ContentDialog
            {
                Title = "Import Failed",
                Content = $"\"{file.Name}\" couldn't be used - either it had no valid \"# Category\" / \"## TAG\" entries, or one of its categories had more than 12 tags. Nothing was imported; still using the previous categories.",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    // Lets you save a writable copy of the packaged, read-only default
    // schema (Assets/DefaultSchema.md) anywhere on disk, so it can be
    // hand-edited and later brought back in via Import Schema. Copied
    // byte-for-byte - see SchemaFileStore.ExportDefaultSchema.
    private async void ExportSchemaMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var picker = new Windows.Storage.Pickers.FileSavePicker();
        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

        picker.SuggestedFileName = "DefaultSchema";
        picker.FileTypeChoices.Add("Markdown", new List<string> { ".md" });
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;

        var file = await picker.PickSaveFileAsync();
        if (file == null)
            return; // user cancelled the picker

        try
        {
            _schemaStore.ExportDefaultSchema(file.Path);
        }
        catch (Exception ex)
        {
            var dialog = new ContentDialog
            {
                Title = "Export Failed",
                Content = $"Couldn't export the default schema: {ex.Message}",
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };
            await dialog.ShowAsync();
        }
    }

    private async void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        var typeRecords = CurrentTypeRecords;
        if (typeRecords.Count == 0)
            return;

        var record = typeRecords[_currentRecordIndex];

        var dialog = new ContentDialog
        {
            Title = "Delete Record?",
            Content = $"Are you sure you want to delete \"{record.NickName}\"? This cannot be undone.",
            PrimaryButtonText = "Delete",
            CloseButtonText = "Cancel",
            XamlRoot = this.Content.XamlRoot
        };

        var result = await dialog.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            _records.Remove(record);
            _store.Save(DataFilePath, _records, _schema);

            if (ReferenceEquals(_pendingNewRecord, record))
                _pendingNewRecord = null;
            _editSnapshotNickName = null;
            _editSnapshotValues = null;

            if (_currentRecordIndex > 0)
                _currentRecordIndex--;

            ShowCurrentRecord();
        }
    }

    // Quits the app. If there's an unsaved edit or an in-progress new
    // record (unlocked), confirm first - same "don't silently lose work"
    // principle as Cancel/navigation elsewhere in the app.
    private async void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_isLocked)
        {
            var dialog = new ContentDialog
            {
                Title = "Discard Unsaved Changes?",
                Content = "You're in the middle of an edit that hasn't been saved. Exit anyway and discard it?",
                PrimaryButtonText = "Exit Without Saving",
                CloseButtonText = "Cancel",
                XamlRoot = this.Content.XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result != ContentDialogResult.Primary)
                return;
        }

        Application.Current.Exit();
    }

    private void LockButton_Click(object sender, RoutedEventArgs e)
    {
        ToggleLock();
    }

    private async void PrintButton_Click(object sender, RoutedEventArgs e)
{
    var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindowInstance);
    await _printService.ShowPrintUIAsync(hwnd, _schema, _records);
}

    // --- Search / Find ---

    private void OpenSearchOverlay()
    {
        _searchOverlayVisible = true;
        SearchOverlay.Visibility = Visibility.Visible;
        SearchTextBox.Text = string.Empty;
        SearchResultsListView.ItemsSource = null;
        SearchTextBox.Focus(FocusState.Programmatic);
    }

    private void CloseSearchOverlay()
    {
        _searchOverlayVisible = false;
        SearchOverlay.Visibility = Visibility.Collapsed;
    }

    // Searches EVERY record, regardless of entity type, for a
    // nickname (NickName) containing the typed text.
    private void RunSearch()
    {
        string term = SearchTextBox.Text?.Trim() ?? string.Empty;

        var matches = _records
            .Where(r => r.NickName.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(r => new RecordListItem(r))
            .ToList();

        SearchResultsListView.ItemsSource = matches;

        if (matches.Count > 0)
        {
            SearchResultsListView.SelectedIndex = 0;
            SearchResultsListView.Focus(FocusState.Programmatic);
        }
    }

    private void NavigateToRecord(EntityRecord record)
    {
        int typeIndex = _schema.FindIndex(e => e.Category == record.Category);
        _currentTypeIndex = typeIndex >= 0 ? typeIndex : 0;

        var typeRecords = CurrentTypeRecords;
        int recordIndex = typeRecords.IndexOf(record);
        _currentRecordIndex = recordIndex >= 0 ? recordIndex : 0;

        CloseSearchOverlay();
        ShowCurrentRecord();
    }

    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            RunSearch();
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CloseSearchOverlay();
            e.Handled = true;
        }
    }

    private void SearchResultsListView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (SearchResultsListView.SelectedItem is RecordListItem item)
            {
                NavigateToRecord(item.Record);
            }
            e.Handled = true;
        }
        else if (e.Key == Windows.System.VirtualKey.Escape)
        {
            CloseSearchOverlay();
            e.Handled = true;
        }
    }

    private void SearchResultsListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecordListItem item)
        {
            NavigateToRecord(item.Record);
        }
    }

    // --- Keyboard navigation for the main screen ---

    private void Page_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (_searchOverlayVisible)
        {
            return;
        }

        bool ctrlDown = InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        if (ctrlDown && e.Key == Windows.System.VirtualKey.F)
        {
            OpenSearchOverlay();
            return;
        }

        if (!_isLocked && e.Key == Windows.System.VirtualKey.Escape)
        {
            CancelEdit();
            return;
        }

        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ToggleLock();
            return;
        }

        // Record/type navigation (keyboard) mirrors the button IsEnabled
        // rules in ShowCurrentRecord: disabled while unlocked/editing, so
        // navigating away can't silently discard unsaved edits. This also
        // means Home/End behave as normal text-editing keys (move cursor
        // to start/end of the focused field) while you're mid-edit, instead
        // of being hijacked for record navigation.
        if (!_isLocked)
        {
            bool isNavKey = e.Key is Windows.System.VirtualKey.Up
                or Windows.System.VirtualKey.Down
                or Windows.System.VirtualKey.Home
                or Windows.System.VirtualKey.End;
            if (isNavKey)
                return;
        }

        if (ctrlDown && e.Key == Windows.System.VirtualKey.Up)
        {
            GoToPreviousType();
            return;
        }

        if (ctrlDown && e.Key == Windows.System.VirtualKey.Down)
        {
            GoToNextType();
            return;
        }

        if (ctrlDown && e.Key == Windows.System.VirtualKey.Home)
        {
            GoToFirstTypeFirstRecord();
            return;
        }

        if (ctrlDown && e.Key == Windows.System.VirtualKey.End)
        {
            GoToLastTypeLastRecord();
            return;
        }

        switch (e.Key)
        {
            case Windows.System.VirtualKey.Up:
                GoToPreviousRecord();
                break;
            case Windows.System.VirtualKey.Down:
                GoToNextRecord();
                break;
            case Windows.System.VirtualKey.Home:
                GoToFirstRecord();
                break;
            case Windows.System.VirtualKey.End:
                GoToLastRecord();
                break;
        }
    }

    // --- Type & record navigation buttons ---
    // Thin wrappers so the same navigation logic serves both the keyboard
    // shortcuts and these buttons - see Page_KeyDown for the Ctrl+arrow-key
    // equivalents.

    private void TypeFirstButton_Click(object sender, RoutedEventArgs e) => GoToFirstTypeFirstRecord();
    private void TypePrevButton_Click(object sender, RoutedEventArgs e) => GoToPreviousType();
    private void TypeNextButton_Click(object sender, RoutedEventArgs e) => GoToNextType();
    private void TypeLastButton_Click(object sender, RoutedEventArgs e) => GoToLastTypeLastRecord();

    private void RecordFirstButton_Click(object sender, RoutedEventArgs e) => GoToFirstRecord();
    private void RecordPrevButton_Click(object sender, RoutedEventArgs e) => GoToPreviousRecord();
    private void RecordNextButton_Click(object sender, RoutedEventArgs e) => GoToNextRecord();
    private void RecordLastButton_Click(object sender, RoutedEventArgs e) => GoToLastRecord();

    private void GoToPreviousRecord()
    {
        if (_currentRecordIndex > 0)
        {
            _currentRecordIndex--;
            ShowCurrentRecord();
        }
    }

    private void GoToNextRecord()
    {
        if (_currentRecordIndex < CurrentTypeRecords.Count - 1)
        {
            _currentRecordIndex++;
            ShowCurrentRecord();
        }
    }

    private void GoToFirstRecord()
    {
        _currentRecordIndex = 0;
        ShowCurrentRecord();
    }

    private void GoToLastRecord()
    {
        _currentRecordIndex = Math.Max(0, CurrentTypeRecords.Count - 1);
        ShowCurrentRecord();
    }

    private void GoToPreviousType()
    {
        if (_currentTypeIndex > 0)
        {
            _currentTypeIndex--;
            _currentRecordIndex = 0;
            ShowCurrentRecord();
        }
    }

    private void GoToNextType()
    {
        if (_currentTypeIndex < _schema.Count - 1)
        {
            _currentTypeIndex++;
            _currentRecordIndex = 0;
            ShowCurrentRecord();
        }
    }

    private void GoToFirstTypeFirstRecord()
    {
        _currentTypeIndex = 0;
        _currentRecordIndex = 0;
        ShowCurrentRecord();
    }

    private void GoToLastTypeLastRecord()
    {
        _currentTypeIndex = _schema.Count - 1;
        _currentRecordIndex = Math.Max(0, CurrentTypeRecords.Count - 1);
        ShowCurrentRecord();
    }
}
