using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Allows to set values to the UI controls without triggering OnValueChanged event.
/// </summary>
/// Useful for avoiding infinite loops.
public static class UIEventSyncExtensions
{
    // empty events
    private static readonly Slider.SliderEvent EmptySliderEvent = new Slider.SliderEvent();
    private static readonly Toggle.ToggleEvent EmptyToggleEvent = new Toggle.ToggleEvent();
    private static readonly InputField.OnChangeEvent EmptyInputFieldEvent = new InputField.OnChangeEvent();
    private static readonly Dropdown.DropdownEvent EmptyDropdownFieldEvent = new Dropdown.DropdownEvent();

    /// <summary>
    /// Sets value to slider without triggering OnValueChanged event.
    /// </summary>
    /// <param name="instance">Slider.</param>
    /// <param name="value">New value.</param>
    public static void SetValue(this Slider instance, float value)
    {
        var originalEvent = instance.onValueChanged;
        instance.onValueChanged = EmptySliderEvent;
        instance.value = value;
        instance.onValueChanged = originalEvent;
    }

    /// <summary>
    /// Sets value to toggle without triggering OnValueChanged event.
    /// </summary>
    /// <param name="instance">Toggle.</param>
    /// <param name="value">New value.</param>
    public static void SetValue(this Toggle instance, bool value)
    {
        var originalEvent = instance.onValueChanged;
        instance.onValueChanged = EmptyToggleEvent;
        instance.isOn = value;
        instance.onValueChanged = originalEvent;
    }

    /// <summary>
    /// Sets value to input field without triggering OnValueChanged event.
    /// </summary>
    /// <param name="instance">InputField.</param>
    /// <param name="value">New value.</param>
    public static void SetValue(this InputField instance, string value)
    {
        var originalEvent = instance.onValueChanged;
        instance.onValueChanged = EmptyInputFieldEvent;
        instance.text = value;
        instance.onValueChanged = originalEvent;
    }

    /// <summary>
    /// Sets value to dropdown without triggering OnValueChanged event.
    /// </summary>
    /// <param name="instance">Dropdown.</param>
    /// <param name="value">New value.</param>
    public static void SetValue(this Dropdown instance, int value)
    {
        var originalEvent = instance.onValueChanged;
        instance.onValueChanged = EmptyDropdownFieldEvent;
        instance.value = value;
        instance.onValueChanged = originalEvent;
    }
}