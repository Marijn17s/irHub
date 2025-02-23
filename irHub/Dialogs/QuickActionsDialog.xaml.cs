using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using irHub.Classes;
using irHub.Classes.Models;
using irHub.Windows;
using Serilog;

namespace irHub.Dialogs;

public partial class QuickActionsDialog : INotifyPropertyChanged
{
    private readonly ObservableCollection<Program> _items = Global.Programs;
    private ObservableCollection<Program> _filteredResults = [];
    private double _resultsHeight;
    private const double MaxWindowHeight = 360;
    private const double BaseHeight = 80;
    private TextBox? SearchTextBox { get; set; }

    public ObservableCollection<Program> FilteredResults
    {
        get => _filteredResults;
        set
        {
            _filteredResults = value;
            OnPropertyChanged();
            AdjustWindowHeight();
        }
    }

    private Program? _selectedResult;
    public Program? SelectedResult
    {
        get => _selectedResult;
        set
        {
            _selectedResult = value;
            OnPropertyChanged();
        }
    }

    public double ResultsHeight
    {
        get => _resultsHeight;
        set
        {
            _resultsHeight = value;
            OnPropertyChanged();
        }
    }

    #region INotifyPropertyChanged
    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
    #endregion

    internal QuickActionsDialog()
    {
        InitializeComponent();
        DataContext = this;

        Log.Information("Opening quick actions dialog..");

        if (Application.Current.MainWindow is MainWindow mainWindow)
            Owner = mainWindow;

        AdjustWindowHeight();
        SetRoundedCorners(this); // todo make a new helper
    }
    
    private void SetRoundedCorners(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        const int dwmwaWindowCornerPreference = 33;
        int dwmWindowCornerRound = 2; // 2 = Rounded, 1 = Default, 0 = No Rounding

        _ = DwmSetWindowAttribute(hwnd, dwmwaWindowCornerPreference, ref dwmWindowCornerRound, sizeof(int));
    }
    
    // todo
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    private void AdjustWindowHeight()
    {
        // Update the list height dynamically with max 5 entries of 40 pixel height each with fixed extra of 20 pixels
        ResultsHeight = 20 + Math.Min(FilteredResults.Count, 5) * 40;
        
        double newHeight = BaseHeight + ResultsHeight;
        Height = Math.Min(newHeight, MaxWindowHeight);
    }

    private void SearchBox_KeyUp(object sender, KeyEventArgs e)
    {
        string query = SearchTextBox?.Text.Trim() ?? "";
        var selectedResult = SelectedResult;
        FilteredResults.Clear();
        
        if (query is not "")
        {
            FilteredResults = new ObservableCollection<Program>(
                _items.Where(i => i.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase))
            );
        }
        AdjustWindowHeight();
        
        if (e.Key is Key.Up or Key.Down)
        {
            Keyboard.Focus(ResultsList);

            var selectedIndex = -1;
            if (selectedResult is not null)
                selectedIndex = FilteredResults.IndexOf(selectedResult);

            if (e.Key is Key.Up && selectedIndex > 0)
                SelectedResult = FilteredResults[selectedIndex - 1];
            if (e.Key is Key.Down && selectedIndex < FilteredResults.Count - 1)
                SelectedResult = FilteredResults[selectedIndex + 1];

            OnPropertyChanged(nameof(SelectedResult));
            if (SelectedResult is not null)
                ResultsList.ScrollIntoView(SelectedResult);

            e.Handled = true;
        }
        else if (e.Key is Key.Enter)
        {
            if (selectedResult is not null)
            {
                var t = new ProgramDialog(ref selectedResult);
                t.ShowDialog();
                Close();
            }
            e.Handled = true;
        }
        else if (FilteredResults.Count > 0)
            SelectedResult = FilteredResults[0];
        
        Keyboard.Focus(SearchTextBox);
        if (SelectedResult is not null)
            ResultsList.ScrollIntoView(SelectedResult);
    }

    private void Dialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        // Focus on the search box
        if (SearchBox.Template.FindName("PART_TextBox", SearchBox) is not TextBox textBox) return;
        
        SearchTextBox = textBox;
        SearchTextBox.Focus();
    }
    
    // todo idea: add actionbar to bottom like 1password shortcuts

    private void QuickActionsDialog_OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape) Close();
    }

    private void SearchBox_OnGotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        SearchBorder.BorderThickness = new Thickness(3);
        if (FilteredResults.Count <= 0 || SelectedResult is not null) return;
        
        SelectedResult = FilteredResults[0];
        OnPropertyChanged(nameof(SelectedResult));
    }

    private void SearchBox_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e) => SearchBorder.BorderThickness = new Thickness(0);
}
