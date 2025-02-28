﻿using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace FluentAvalonia.UI.Controls;

public class FAComboBoxAutomationPeer : SelectingItemsControlAutomationPeer,
    IExpandCollapseProvider, IValueProvider
{
    public FAComboBoxAutomationPeer(SelectingItemsControl owner) 
        : base(owner)
    {

    }

    public new FAComboBox Owner => (FAComboBox)base.Owner;

    public ExpandCollapseState ExpandCollapseState => ToState(Owner.IsDropDownOpen);

    public bool ShowsMenu => true;

    bool IValueProvider.IsReadOnly => true;

    string IValueProvider.Value
    {
        get
        {
            var selection = GetSelection();
            return selection.Count == 1 ? selection[0].GetName() : null;
        }
    }

    public void Collapse() => Owner.IsDropDownOpen = false;

    public void Expand() => Owner.IsDropDownOpen = true;

    public void SetValue(string value) => throw new NotSupportedException();

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.ComboBox;

    protected override IReadOnlyList<AutomationPeer> GetSelectionCore()
    {
        if (ExpandCollapseState == ExpandCollapseState.Expanded)
            return base.GetSelectionCore();

        // If the combo box is not open then we won't have an ItemsPresenter so the default
        // GetSelectionCore implementation won't work. For this case we create a separate
        // peer to represent the unrealized item.
        if (Owner.SelectedItem is object selection)
        {
            _selection ??= new[] { new UnrealizedSelectionPeer(this) };
            _selection[0].Item = selection;
            return _selection;
        }

        return null;
    }

    protected override void OwnerPropertyChanged(object sender, AvaloniaPropertyChangedEventArgs e)
    {
        base.OwnerPropertyChanged(sender, e);

        if (e.Property == FAComboBox.IsDropDownOpenProperty)
        {
            RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                ToState((bool)e.OldValue!),
                ToState((bool)e.NewValue!));
        }
    }

    private static ExpandCollapseState ToState(bool value)
    {
        return value ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;
    }

    private UnrealizedSelectionPeer[] _selection;

    private class UnrealizedSelectionPeer : UnrealizedElementAutomationPeer
    {
        public UnrealizedSelectionPeer(FAComboBoxAutomationPeer owner)
        {
            _owner = owner;
        }

        public object Item
        {
            get => _item;
            set
            {
                if (_item != value)
                {
                    var oldValue = GetNameCore();
                    _item = value;
                    RaisePropertyChangedEvent(
                        AutomationElementIdentifiers.NameProperty,
                        oldValue,
                        GetNameCore());
                }
            }
        }

        protected override string GetAcceleratorKeyCore() => null;
        protected override string GetAccessKeyCore() => null;
        protected override string GetAutomationIdCore() => null;
        protected override string GetClassNameCore() => typeof(FAComboBoxItem).Name;
        protected override AutomationPeer GetLabeledByCore() => null;
        protected override AutomationPeer GetParentCore() => _owner;
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;

        protected override string GetNameCore()
        {
            if (_item is Control c)
            {
                var result = AutomationProperties.GetName(c);

                if (result is null && c is ContentControl cc && cc.Presenter?.Child is TextBlock text)
                {
                    result = text.Text;
                }

                if (result is null)
                {
                    result = c.GetValue(ContentControl.ContentProperty)?.ToString();
                }

                return result;
            }

            return _item?.ToString();
        }

        private readonly FAComboBoxAutomationPeer _owner;
        private object _item;
    }
}
