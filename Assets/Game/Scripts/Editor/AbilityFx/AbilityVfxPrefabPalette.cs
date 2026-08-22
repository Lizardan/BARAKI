using System;
using System.Collections.Generic;
using Game.Gameplay.Vfx;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Embedded live-thumb palette for BARAKI Studio. Not an EditorWindow.</summary>
    public sealed class AbilityVfxPrefabPalette : IDisposable
    {
        const int PoolSize = 8;
        const int PoolCap = 48;
        const int MaxColumns = 8;
        const float MinCellWidth = 132f;
        const float LabelHeight = 20f;
        const float CellPad = 8f;
        const float SidePad = 8f;
        const float ScrollGutter = 16f;

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
        bool _disposed;

        enum KindFilter
        {
            All = 0,
            Cast = 1,
            Hit = 2,
            Aura = 3,
            Custom = 4,
        }

        public int FilteredCount => _filtered.Count;

        public GameObject Current => _current;

        public void Bind(Action<GameObject> onPicked) => _onPicked = onPicked;

        public void SetIndex(IReadOnlyList<AbilityVfxPrefabIndex.Entry> index)
        {
            _index = index != null
                ? new List<AbilityVfxPrefabIndex.Entry>(index)
                : AbilityVfxPrefabIndex.Scan();
            RebuildFilter();
        }

        public void Sync(GameObject current) => _current = current;

        public void Tick(float dt)
        {
            if (_disposed || _pool == null)
            {
                return;
            }

            for (var i = 0; i < _pool.Length; i++)
            {
                _pool[i]?.Tick(dt);
            }
        }

        public void DrawFilters()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.BeginChangeCheck();
            _search = GUILayout.TextField(_search ?? "", EditorStyles.toolbarSearchField);
            if (EditorGUI.EndChangeCheck())
            {
                RebuildFilter();
            }

            GUILayout.Label($"{_filtered.Count}", EditorStyles.miniLabel, GUILayout.Width(36f));
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            DrawKindChip("Все", KindFilter.All);
            DrawKindChip("Cast", KindFilter.Cast);
            DrawKindChip("Hit", KindFilter.Hit);
            DrawKindChip("Aura", KindFilter.Aura);
            DrawKindChip("Кастом", KindFilter.Custom);
            EditorGUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            var dragged = (GameObject)EditorGUILayout.ObjectField(
                _current,
                typeof(GameObject),
                false);
            if (EditorGUI.EndChangeCheck())
            {
                Pick(dragged);
            }

            EditorGUILayout.EndVertical();
        }

        public void DrawGrid()
        {
            EnsurePool();
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

        public void ClearThumbs() => ClearPoolBindings();

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            DisposePool();
            _onPicked = null;
        }

        void DrawKindChip(string label, KindFilter filter)
        {
            var prev = GUI.backgroundColor;
            if (_kindFilter == filter)
            {
                GUI.backgroundColor = new Color(0.45f, 0.72f, 1f, 1f);
            }

            if (GUILayout.Button(label, EditorStyles.miniButton))
            {
                _kindFilter = filter;
                RebuildFilter();
            }

            GUI.backgroundColor = prev;
        }

        void DrawGrid(Rect view)
        {
            var usable = Mathf.Max(MinCellWidth, view.width - ScrollGutter - SidePad * 2f);
            var columns = Mathf.Clamp(Mathf.FloorToInt(usable / MinCellWidth), 1, MaxColumns);
            var cellW = usable / columns;
            var thumbSide = cellW - CellPad;
            var cellH = LabelHeight + thumbSide + CellPad;
            var total = _filtered.Count + 1;
            var rows = Mathf.Max(1, Mathf.CeilToInt(total / (float)columns));
            var content = new Rect(0f, 0f, SidePad * 2f + columns * cellW, SidePad + rows * cellH);

            _scroll = GUI.BeginScrollView(view, _scroll, content);
            var visibleMin = _scroll.y;
            var visibleMax = _scroll.y + view.height;
            var visible = new List<int>();

            DrawNoneCell(new Rect(SidePad, SidePad, cellW - CellPad, cellH - CellPad));

            for (var i = 0; i < _filtered.Count; i++)
            {
                var slot = i + 1;
                var col = slot % columns;
                var row = slot / columns;
                var rect = new Rect(
                    SidePad + col * cellW,
                    SidePad + row * cellH,
                    cellW - CellPad,
                    cellH - CellPad);
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
                var rect = new Rect(
                    SidePad + col * cellW,
                    SidePad + row * cellH,
                    cellW - CellPad,
                    cellH - CellPad);
                DrawPrefabCell(rect, i, _index[_filtered[i]]);
            }

            GUI.EndScrollView();
        }

        void DrawNoneCell(Rect rect)
        {
            var thumb = new Rect(rect.x, rect.y + LabelHeight, rect.width, rect.width);
            if (_current == null)
            {
                EditorGUI.DrawRect(
                    new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 2f),
                    new Color(0.3f, 0.55f, 0.9f, 0.55f));
            }

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
            _current = prefab;
            _onPicked?.Invoke(prefab);
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
                KindFilter.Custom => kind == AbilityVfxKind.Custom,
                _ => true,
            };

        void EnsurePool() => EnsurePoolSize(PoolSize);

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
            if (_poolBound == null)
            {
                return false;
            }

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
