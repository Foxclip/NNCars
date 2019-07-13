using UnityEngine;

/// <summary>
/// Can be attached to GameObject as a component.
/// </summary>
public class TextProperty : MonoBehaviour
{
    /// <summary>
    /// Value of the property.
    /// </summary>
    public string Text { get; set; } = string.Empty;
}
