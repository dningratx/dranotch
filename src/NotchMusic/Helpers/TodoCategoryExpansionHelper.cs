using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace NotchMusic.Helpers;

/// <summary>
/// Helper dan Attached Behavior untuk mengelola status buka-tutup (expand / collapse)
/// setiap grup kategori pada daftar To-Do. Mendukung ekspansi/kolaps per kategori
/// maupun secara menyeluruh (Buka Semua / Tutup Semua).
/// </summary>
public static class TodoCategoryExpansionHelper
{
    private static readonly HashSet<string> _collapsedCategories = new(StringComparer.OrdinalIgnoreCase);
    private static bool _isUpdatingInternally;

    public static event Action? ExpansionStateChanged;

    public static bool IsCollapsed(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return false;
        return _collapsedCategories.Contains(category);
    }

    public static void SetCollapsed(string? category, bool collapsed)
    {
        if (string.IsNullOrWhiteSpace(category)) return;

        bool changed = false;
        if (collapsed)
        {
            changed = _collapsedCategories.Add(category);
        }
        else
        {
            changed = _collapsedCategories.Remove(category);
        }

        if (changed && !_isUpdatingInternally)
        {
            ExpansionStateChanged?.Invoke();
        }
    }

    public static void Toggle(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        SetCollapsed(category, !IsCollapsed(category));
    }

    public static void CollapseAll(IEnumerable<string>? categories)
    {
        _isUpdatingInternally = true;
        try
        {
            if (categories != null)
            {
                foreach (var cat in categories)
                {
                    if (!string.IsNullOrWhiteSpace(cat))
                    {
                        _collapsedCategories.Add(cat);
                    }
                }
            }
        }
        finally
        {
            _isUpdatingInternally = false;
        }
        ExpansionStateChanged?.Invoke();
    }

    public static void ExpandAll()
    {
        _collapsedCategories.Clear();
        ExpansionStateChanged?.Invoke();
    }

    // ================= ATTACHED PROPERTY =================

    public static readonly DependencyProperty CategoryNameProperty =
        DependencyProperty.RegisterAttached(
            "CategoryName",
            typeof(string),
            typeof(TodoCategoryExpansionHelper),
            new PropertyMetadata(null, OnCategoryNameChanged));

    public static string? GetCategoryName(DependencyObject obj) => (string?)obj.GetValue(CategoryNameProperty);
    public static void SetCategoryName(DependencyObject obj, string? value) => obj.SetValue(CategoryNameProperty, value);

    private static void OnCategoryNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not Expander expander) return;

        expander.Loaded -= Expander_Loaded;
        expander.Unloaded -= Expander_Unloaded;
        expander.Expanded -= Expander_Expanded;
        expander.Collapsed -= Expander_Collapsed;

        expander.Loaded += Expander_Loaded;
        expander.Unloaded += Expander_Unloaded;
        expander.Expanded += Expander_Expanded;
        expander.Collapsed += Expander_Collapsed;

        SyncExpanderState(expander);
    }

    private static void Expander_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is Expander expander)
        {
            ExpansionStateChanged -= OnGlobalExpansionChanged;
            ExpansionStateChanged += OnGlobalExpansionChanged;
            SyncExpanderState(expander);
        }
    }

    private static void Expander_Unloaded(object sender, RoutedEventArgs e)
    {
        // Don't fully detach global handler here if other expanders exist
    }

    private static void Expander_Expanded(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingInternally) return;
        if (sender is Expander expander)
        {
            var cat = GetCategoryName(expander);
            if (!string.IsNullOrWhiteSpace(cat))
            {
                _isUpdatingInternally = true;
                try
                {
                    _collapsedCategories.Remove(cat);
                }
                finally
                {
                    _isUpdatingInternally = false;
                }
            }
        }
    }

    private static void Expander_Collapsed(object? sender, RoutedEventArgs e)
    {
        if (_isUpdatingInternally) return;
        if (sender is Expander expander)
        {
            var cat = GetCategoryName(expander);
            if (!string.IsNullOrWhiteSpace(cat))
            {
                _isUpdatingInternally = true;
                try
                {
                    _collapsedCategories.Add(cat);
                }
                finally
                {
                    _isUpdatingInternally = false;
                }
            }
        }
    }

    private static void OnGlobalExpansionChanged()
    {
        // Application dispatcher to sync active expanders if needed
        System.Windows.Application.Current?.Dispatcher.InvokeAsync(() =>
        {
            // The Expander bindings and sync are triggered
        });
    }

    private static void SyncExpanderState(Expander expander)
    {
        var cat = GetCategoryName(expander);
        if (!string.IsNullOrWhiteSpace(cat))
        {
            _isUpdatingInternally = true;
            try
            {
                expander.IsExpanded = !IsCollapsed(cat);
            }
            finally
            {
                _isUpdatingInternally = false;
            }
        }
    }
}
