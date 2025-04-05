﻿using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using irHub.Classes;
using irHub.Classes.Enums;
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
    private const double SuggestionsHeight = 42;
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
    }

    private void AdjustWindowHeight()
    {
        // Update the list height dynamically with max 5 entries of 40 pixel height each with fixed extra of 20 pixels
        ResultsHeight = 20 + Math.Min(FilteredResults.Count, 5) * 40;

        double newHeight = 0;
        SuggestionsBox.Visibility = Visibility.Collapsed;
        
        if (FilteredResults.Count > 0)
        {
            newHeight = SuggestionsHeight;
            SuggestionsBox.Visibility = Visibility.Visible;
        }
        
        newHeight += BaseHeight + ResultsHeight;
        Height = Math.Min(newHeight, MaxWindowHeight);
    }

    private async void SearchBox_KeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Tab)
        {
            Keyboard.Focus(SearchTextBox);
            e.Handled = true;
            return;
        }
        
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
            else if (e.Key is Key.Up && selectedIndex is 0)
                SelectedResult = FilteredResults[^1];

            OnPropertyChanged(nameof(SelectedResult));
            if (SelectedResult is not null)
                ResultsList.ScrollIntoView(SelectedResult);

            e.Handled = true;
        }
        else if (e.Key is Key.Enter)
        {
            if (selectedResult is not null)
            {
                if (e.KeyboardDevice.Modifiers.HasFlag(ModifierKeys.Shift))
                {
                    var t = new ProgramDialog(ref selectedResult);
                    t.ShowDialog();
                    e.Handled = true;
                    Close();
                    return;
                }
                if (selectedResult.State is ProgramState.Running)
                    await Global.StopProgram(selectedResult);
                else if (selectedResult.State is ProgramState.Stopped)
                    await Global.StartProgram(selectedResult);
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
    
    private void ClearButton_Click(object sender, RoutedEventArgs e) => SearchTextBox?.Clear();

    private void Dialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        // Focus on the search box
        if (SearchBox.Template.FindName("PART_TextBox", SearchBox) is not TextBox textBox) return;
        
        SearchTextBox = textBox;
        Keyboard.Focus(SearchTextBox);
    }

    private void QuickActionsDialog_OnKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape)
            Owner.Focus();
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
