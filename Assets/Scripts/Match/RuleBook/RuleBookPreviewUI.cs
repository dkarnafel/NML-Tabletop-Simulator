using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;

public class RuleBookPreviewUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform contentRoot;
    [SerializeField] private Image pagePrefab;

    [Header("PDF Pages")]
    [Tooltip("Folder INSIDE Assets/Resources. Example: Rulebook")]
    [SerializeField] private string resourcesFolder = "Rulebook";

    [SerializeField] private float maxPageWidth = 700f;

    private readonly List<GameObject> spawnedPages = new();
    private bool isOpen;

    private static readonly Regex numberRegex = new Regex(@"\d+", RegexOptions.Compiled);

    private void Awake()
    {
        if (panelRoot == null)
            Debug.LogError("[RuleBookPreviewUI] panelRoot is not assigned.");

        if (contentRoot == null)
            Debug.LogError("[RuleBookPreviewUI] contentRoot is not assigned.");

        if (pagePrefab == null)
            Debug.LogError("[RuleBookPreviewUI] pagePrefab is not assigned.");

        // Panel MUST start hidden
        if (panelRoot != null)
            panelRoot.SetActive(false);

        isOpen = false;
    }

    // === BUTTON ENTRY POINT ===
    public void Toggle()
    {
        if (isOpen) Close();
        else Open();
    }

    public void Open()
    {
        if (isOpen) return;
        isOpen = true;

        if (panelRoot != null)
            panelRoot.SetActive(true);

        LoadPages();
    }

    public void Close()
    {
        if (!isOpen) return;
        isOpen = false;

        ClearPages();

        if (panelRoot != null)
            panelRoot.SetActive(false);
    }

    private void LoadPages()
    {
        ClearPages();

        if (contentRoot == null || pagePrefab == null)
            return;

        // IMPORTANT: This ONLY loads from Assets/Resources/<resourcesFolder>
        Sprite[] pages = Resources.LoadAll<Sprite>(resourcesFolder);

        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning(
                $"[RuleBookPreviewUI] No pages found. " +
                $"Put your sprites in Assets/Resources/{resourcesFolder}/ " +
                $"and ensure Texture Type = Sprite (2D and UI).");
            return;
        }

        Array.Sort(pages, CompareByEmbeddedNumberThenName);

        foreach (var sprite in pages)
        {
            var img = Instantiate(pagePrefab, contentRoot);
            img.sprite = sprite;
            img.preserveAspect = true;

            float aspect = (float)sprite.rect.height / sprite.rect.width;
            float width = maxPageWidth;
            float height = width * aspect;

            RectTransform rt = img.rectTransform;
            rt.sizeDelta = new Vector2(width, height);

            var layout = img.GetComponent<LayoutElement>();
            if (layout != null)
            {
                layout.preferredWidth = width;
                layout.preferredHeight = height;
            }

            spawnedPages.Add(img.gameObject);
        }
    }

    private static int CompareByEmbeddedNumberThenName(Sprite a, Sprite b)
    {
        int na = ExtractFirstNumber(a.name);
        int nb = ExtractFirstNumber(b.name);

        // If both have numbers, sort by number first
        if (na != int.MinValue && nb != int.MinValue)
        {
            int cmp = na.CompareTo(nb);
            if (cmp != 0) return cmp;
        }

        // Fallback to name
        return string.CompareOrdinal(a.name, b.name);
    }

    private static int ExtractFirstNumber(string s)
    {
        var m = numberRegex.Match(s);
        if (!m.Success) return int.MinValue;
        if (int.TryParse(m.Value, out int n)) return n;
        return int.MinValue;
    }

    private void ClearPages()
    {
        for (int i = 0; i < spawnedPages.Count; i++)
        {
            if (spawnedPages[i] != null)
                Destroy(spawnedPages[i]);
        }
        spawnedPages.Clear();
    }
}
