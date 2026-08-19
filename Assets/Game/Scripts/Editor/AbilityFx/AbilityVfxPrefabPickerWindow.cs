using System;
using System.Collections.Generic;
using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Scrollable grid of live VFX thumbs. Click a cell to pick the prefab.</summary>
    public sealed class AbilityVfxPrefabPickerWindow : EditorWindow
    {
        const int PoolSize = 8;
        const int PoolCap = 48;
        const int MaxColumns = 5;
        const float MinCellWidth = 176f;
        const float LabelHeight = 20f;
        const float CellPad = 8f;

        List<AbilityVfxPrefabIndex.Entry> _index = new();
        readonly List<int> _filtered = new();
        AbilityVfxThumbSession[] _pool;
        int[] _poolBound;
        Vector2 _scroll;
        Rect _cachedView;
        string _search = "";
        KindFilter _kindFilter = KindFilter.All;
        GameObject _current;
        Action<GameObject> _onPicked;
        double _lastTick;

        enum KindFilter
        {
            All = 0,
            Cast = 1,
            Hit = 2,
            Aura = 3,
        }

        public static void Open(
            IReadOnlyList<AbilityVfxPrefabIndex.Entry> index,
            AbilityVfxKind defaultKind,
            GameObject current,
            Action<GameObject> onPicked)
        {
            var window = GetWindow<AbilityVfxPrefabPickerWindow>(true, "Эффект", true);
            window.minSize = new Vector2(640f, 480f);
            window._index = index != null
                ? new List<AbilityVfxPrefabIndex.Entry>(index)
                : AbilityVfxPrefabIndex.Scan();
            window._current = current;
            window._onPicked = onPicked;
            window._kindFilter = defaultKind switch
            {
                AbilityVfxKind.Hit => KindFilter.Hit,
                AbilityVfxKind.Aura => KindFilter.Aura,
                AbilityVfxKind.Cast => KindFilter.Cast,
                _ => KindFilter.All,
            };
            window._search = "";
            window._scroll = Vector2.zero;
            window.EnsurePool();
            window.RebuildFilter();
            window.Show();
            window.Focus();
        }

        void OnEnable()
        {
            _lastTick = EditorApplication.timeSinceStartup;
            EditorApplication.update += OnEditorUpdate;
            EnsurePool();
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            DisposePool();
        }

        void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var dt = (float)(now - _lastTick);
            _lastTick = now;
            if (_pool == null)
            {
                return;
            }

            for (var i = 0; i < _pool.Length; i++)
            {
                _pool[i]?.Tick(dt);
            }

            Repaint();
        }

        void OnGUI()
        {
            DrawToolbar();
            var view = GUILayoutUtility.GetRect(
                16f,
                16f,
                GUILayout.ExpandWidth(true),
                GUILayout.ExpandHeight(true));
            if (Event.current.type == EventType.Repaint && view.height > 32f)
            {
                _cachedView = view;
            }

            DrawGrid(view.height > 32f ? view : _cachedView.width > 32f ? _cachedView : view);
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            _search = GUILayout.TextField(_search ?? "", EditorStyles.toolbarSearchField, GUILayout.MinWidth(160f));
            if (EditorGUI.EndChangeCheck())
            {
                RebuildFilter();
            }

            DrawKindChip("Все", KindFilter.All);
            DrawKindChip("Cast", KindFilter.Cast);
            DrawKindChip("Hit", KindFilter.Hit);
            DrawKindChip("Aura", KindFilter.Aura);
            GUILayout.FlexibleSpace();
            GUILayout.Label($"{_filtered.Count}", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
        }

        void DrawKindChip(string label, KindFilter filter)
        {
            var prev = GUI.backgroundColor;
            if (_kindFilter == filter)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            if (GUILayout.Button(label, EditorStyles.toolbarButton, GUILayout.Width(48f)))
            {
                _kindFilter = filter;
                RebuildFilter();
            }

            GUI.backgroundColor = prev;
        }

        void DrawGrid(Rect view)
        {
            var usable = Mathf.Max(MinCellWidth, view.width - 18f);
            var columns = Mathf.Clamp(Mathf.FloorToInt(usable / MinCellWidth), 1, MaxColumns);
            var cellW = usable / columns;
            var thumbSide = cellW - CellPad;
            var cellH = LabelHeight + thumbSide + CellPad;
            var total = _filtered.Count + 1;
            var rows = Mathf.Max(1, Mathf.CeilToInt(total / (float)columns));
            var content = new Rect(0f, 0f, columns * cellW, rows * cellH);

            _scroll = GUI.BeginScrollView(view, _scroll, content);
            var visibleMin = _scroll.y;
            var visibleMax = _scroll.y + view.height;
            var visible = new List<int>();

            DrawNoneCell(new Rect(0f, 0f, cellW - CellPad, cellH - CellPad));

            for (var i = 0; i < _filtered.Count; i++)
            {
                var slot = i + 1;
                var col = slot % columns;
                var row = slot / columns;
                var rect = new Rect(col * cellW, row * cellH, cellW - CellPad, cellH - CellPad);
                if (rect.yMax < visibleMin - cellH || rect.yMin > visibleMax + cellH)
                {
                    continue;
                }

                visible.Add(i);
            }

            if (Event.current.type == EventType.Repaint)
            {
                RebindPool(visible);
            }

            for (var v = 0; v < visible.Count; v++)
            {
                var i = visible[v];
                var slot = i + 1;
                var col = slot % columns;
                var row = slot / columns;
                var rect = new Rect(col * cellW, row * cellH, cellW - CellPad, cellH - CellPad);
                DrawPrefabCell(rect, i, _index[_filtered[i]]);
            }

            GUI.EndScrollView();
        }

        void DrawNoneCell(Rect rect)
        {
            var thumb = new Rect(rect.x, rect.y + LabelHeight, rect.width, rect.width);
            EditorGUI.DrawRect(thumb, new Color(0.16f, 0.16f, 0.18f, 1f));
            GUI.Label(new Rect(rect.x, rect.y, rect.width, LabelHeight), "нет", EditorStyles.centeredGreyMiniLabel);
            if (GUI.Button(thumb, GUIContent.none, GUIStyle.none))
            {
                Pick(null);
            }
        }

        void DrawPrefabCell(Rect rect, int filteredIndex, AbilityVfxPrefabIndex.Entry entry)
        {
            var labelRect = new Rect(rect.x, rect.y, rect.width, LabelHeight);
            var thumbRect = new Rect(rect.x, rect.y + LabelHeight, rect.width, rect.width);
            var selected = entry.Prefab == _current;
            if (selected)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 2f),
                    new Color(0.3f, 0.55f, 0.9f, 0.55f));
            }

            var name = entry.DisplayName ?? "";
            GUI.Label(labelRect, name, EditorStyles.miniLabel);

            var session = FindBound(filteredIndex);
            if (session != null && Event.current.type == EventType.Repaint)
            {
                GUI.DrawTexture(thumbRect, session.Target, ScaleMode.ScaleToFit, false);
            }
            else
            {
                EditorGUI.DrawRect(thumbRect, new Color(0.14f, 0.14f, 0.16f, 1f));
            }

            if (GUI.Button(thumbRect, GUIContent.none, GUIStyle.none))
            {
                Pick(entry.Prefab);
            }
        }

        void Pick(GameObject prefab)
        {
            _onPicked?.Invoke(prefab);
            Close();
        }

        void RebuildFilter()
        {
            _filtered.Clear();
            var search = (_search ?? "").Trim();
            for (var i = 0; i < _index.Count; i++)
            {
                var entry = _index[i];
                if (!MatchesKind(entry.Kind))
                {
                    continue;
                }

                if (search.Length > 0
                    && entry.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                _filtered.Add(i);
            }

            ClearPoolBindings();
        }

        bool MatchesKind(AbilityVfxKind kind) =>
            _kindFilter switch
            {
                KindFilter.Cast => kind == AbilityVfxKind.Cast,
                KindFilter.Hit => kind == AbilityVfxKind.Hit,
                KindFilter.Aura => kind == AbilityVfxKind.Aura,
                _ => true,
            };

        void EnsurePool()
        {
            EnsurePoolSize(PoolSize);
        }

        void EnsurePoolSize(int needed)
        {
            needed = Mathf.Clamp(needed, 1, PoolCap);
            if (_pool != null && _pool.Length >= needed)
            {
                return;
            }

            var oldPool = _pool;
            var oldBound = _poolBound;
            var oldLen = oldPool != null ? oldPool.Length : 0;
            _pool = new AbilityVfxThumbSession[needed];
            _poolBound = new int[needed];
            for (var i = 0; i < needed; i++)
            {
                if (i < oldLen)
                {
                    _pool[i] = oldPool[i];
                    _poolBound[i] = oldBound[i];
                }
                else
                {
                    _pool[i] = new AbilityVfxThumbSession();
                    _poolBound[i] = -1;
                }
            }
        }

        void RebindPool(List<int> visibleFiltered)
        {
            EnsurePoolSize(visibleFiltered.Count);
            if (_pool == null)
            {
                return;
            }

            var visibleSet = new HashSet<int>(visibleFiltered);
            for (var i = 0; i < _pool.Length; i++)
            {
                var bound = _poolBound[i];
                if (bound >= 0 && !visibleSet.Contains(bound))
                {
                    _pool[i].Clear();
                    _poolBound[i] = -1;
                }
            }

            for (var v = 0; v < visibleFiltered.Count; v++)
            {
                var filteredIndex = visibleFiltered[v];
                if (IsBound(filteredIndex))
                {
                    continue;
                }

                var slot = FreeSlot();
                if (slot < 0)
                {
                    continue;
                }

                _pool[slot].Bind(_index[_filtered[filteredIndex]].Prefab);
                _poolBound[slot] = filteredIndex;
            }
        }

        void ClearPoolBindings()
        {
            if (_pool == null)
            {
                return;
            }

            for (var i = 0; i < _pool.Length; i++)
            {
                _pool[i]?.Clear();
                _poolBound[i] = -1;
            }
        }

        bool IsBound(int filteredIndex)
        {
            for (var i = 0; i < _poolBound.Length; i++)
            {
                if (_poolBound[i] == filteredIndex)
                {
                    return true;
                }
            }

            return false;
        }

        int FreeSlot()
        {
            for (var i = 0; i < _poolBound.Length; i++)
            {
                if (_poolBound[i] < 0)
                {
                    return i;
                }
            }

            return -1;
        }

        AbilityVfxThumbSession FindBound(int filteredIndex)
        {
            if (_pool == null || filteredIndex < 0)
            {
                return null;
            }

            for (var i = 0; i < _poolBound.Length; i++)
            {
                if (_poolBound[i] == filteredIndex)
                {
                    return _pool[i];
                }
            }

            return null;
        }

        void DisposePool()
        {
            if (_pool == null)
            {
                return;
            }

            for (var i = 0; i < _pool.Length; i++)
            {
                _pool[i]?.Dispose();
                _pool[i] = null;
            }

            _pool = null;
            _poolBound = null;
        }
    }
}
