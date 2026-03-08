/**
 * Invoicetronic Desk — AG Grid shared helpers
 */

/** Converts camelCase to snake_case for API sort params. */
function toSnakeCase(str) {
    return str.replace(/[A-Z]/g, letter => '_' + letter.toLowerCase());
}

/**
 * Desk theme for AG Grid v35+ (Theming API).
 * Based on Quartz with brand colors/fonts.
 */
const deskTheme = agGrid.themeQuartz
    .withParams({
        backgroundColor: '#ffffff',
        foregroundColor: '#1a2e2b',
        accentColor: '#5BBA91',
        headerBackgroundColor: '#edf4f1',
        headerTextColor: '#092B28',
        headerFontWeight: 600,
        headerFontSize: 12,
        borderColor: 'rgba(9, 43, 40, 0.08)',
        rowHoverColor: 'rgba(91, 186, 145, 0.06)',
        selectedRowBackgroundColor: 'rgba(91, 186, 145, 0.1)',
        fontFamily: "'Inter', -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif",
        fontSize: 13,
        spacing: 5,
        rowHeight: 42,
        headerHeight: 40,
        cellHorizontalPadding: 16,
        wrapperBorderRadius: 0,
        borders: false,
        rowBorder: true,
        columnBorder: false,
        inputFocusBorder: { color: '#5BBA91' },
        checkboxCheckedBackgroundColor: '#5BBA91',
        checkboxCheckedBorderColor: '#5BBA91',
    });

/**
 * Creates a server-side datasource for AG Grid infinite row model.
 * @param {string} handlerUrl - The Razor Pages handler URL (e.g., '/Companies?handler=List')
 * @param {function} [extraParamsFn] - Optional function returning extra query params object
 * @returns {object} AG Grid datasource
 */
function createDatasource(handlerUrl, extraParamsFn) {
    return {
        getRows(params) {
            const page = Math.floor(params.startRow / pageSize(params)) + 1;
            const size = pageSize(params);

            let sort = null;
            if (params.sortModel && params.sortModel.length > 0) {
                const s = params.sortModel[0];
                sort = (s.sort === 'desc' ? '-' : '') + toSnakeCase(s.colId);
            }

            const queryParams = new URLSearchParams();
            queryParams.set('page', page);
            queryParams.set('pageSize', size);
            if (sort) queryParams.set('sort', sort);

            // Add extra params (e.g., filters)
            if (extraParamsFn) {
                const extra = extraParamsFn();
                for (const [key, value] of Object.entries(extra)) {
                    if (value !== null && value !== undefined && value !== '') {
                        queryParams.set(key, value);
                    }
                }
            }

            const separator = handlerUrl.includes('?') ? '&' : '?';
            const url = handlerUrl + separator + queryParams.toString();

            fetch(url)
                .then(response => {
                    if (!response.ok) throw new Error(`HTTP ${response.status}`);
                    return response.json();
                })
                .then(result => {
                    params.successCallback(result.data || [], result.totalCount || 0);
                })
                .catch(() => {
                    params.failCallback();
                });
        }
    };
}

function pageSize(params) {
    return params.endRow - params.startRow;
}

/**
 * Returns default grid options for consistent styling.
 * @param {object} [overrides] - Override any default option
 * @returns {object} AG Grid options
 */
function defaultGridOptions(overrides) {
    return Object.assign({
        theme: deskTheme,
        rowModelType: 'infinite',
        cacheBlockSize: 20,
        maxBlocksInCache: 10,
        pagination: true,
        paginationPageSize: 20,
        animateRows: true,
        suppressCellFocus: true,
        domLayout: 'autoHeight',
        defaultColDef: {
            sortable: true,
            resizable: true,
            minWidth: 80,
        },
    }, overrides || {});
}

/**
 * Initialize an AG Grid on a container element.
 * @param {string} containerId - The DOM element ID
 * @param {object} gridOptions - AG Grid options (should include columnDefs and datasource)
 * @returns {object} The grid API
 */
function initGrid(containerId, gridOptions) {
    const container = document.getElementById(containerId);
    if (!container) {
        console.error('Grid container not found:', containerId);
        return null;
    }

    // Snapshot defaults, then restore saved state (order + visibility)
    const storageKey = 'desk-grid-cols-' + containerId;
    if (gridOptions.columnDefs) {
        gridOptions.columnDefs.forEach((col, i) => {
            col._defaultHide = !!col.hide;
            col._defaultIndex = i;
        });
        const saved = loadColumnState(storageKey);
        if (saved) {
            applyColumnState(gridOptions.columnDefs, saved);
        }
    }

    const options = defaultGridOptions(gridOptions);
    const gridApi = agGrid.createGrid(container, options);
    return gridApi;
}

/** Column id helper. */
function colId(col) {
    return col.field || col.headerName;
}

/**
 * Applies saved state (order + visibility) to columnDefs array in-place.
 * @param {Array} columnDefs
 * @param {Array} saved - [{id, visible}, ...]
 */
function applyColumnState(columnDefs, saved) {
    if (!Array.isArray(saved)) return;
    const orderMap = {};
    saved.forEach((entry, i) => {
        orderMap[entry.id] = { index: i, visible: entry.visible };
    });
    columnDefs.forEach(col => {
        const id = colId(col);
        if (id && id in orderMap) {
            col.hide = !orderMap[id].visible;
        }
    });
    // Reorder: saved columns first (in saved order), then any new columns not in saved state
    columnDefs.sort((a, b) => {
        const ai = colId(a) in orderMap ? orderMap[colId(a)].index : 9999 + (a._defaultIndex || 0);
        const bi = colId(b) in orderMap ? orderMap[colId(b)].index : 9999 + (b._defaultIndex || 0);
        return ai - bi;
    });
}

/**
 * Builds current column state from grid API.
 * @param {object} gridApi
 * @returns {Array} [{id, visible}, ...]
 */
function getColumnState(gridApi) {
    return gridApi.getColumnDefs()
        .map(col => ({ id: colId(col), visible: !col.hide }))
        .filter(entry => entry.id);
}

/**
 * Loads column state from localStorage.
 * @param {string} key
 * @returns {Array|null}
 */
function loadColumnState(key) {
    try {
        const raw = localStorage.getItem(key);
        return raw ? JSON.parse(raw) : null;
    } catch { return null; }
}

/**
 * Saves column state to localStorage.
 * @param {string} key
 * @param {Array} state - [{id, visible}, ...]
 */
function saveColumnState(key, state) {
    try { localStorage.setItem(key, JSON.stringify(state)); } catch {}
}

/* Column chooser SVG icon (static, safe) */
const _colChooserSvg = '<svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round"><rect x="3" y="3" width="7" height="7"/><rect x="14" y="3" width="7" height="7"/><rect x="3" y="14" width="7" height="7"/><rect x="14" y="14" width="7" height="7"/></svg>';

/**
 * Creates a column chooser button and attaches it to the grid toolbar.
 * @param {string} containerId - The grid container DOM ID (used as storage key)
 * @param {object} gridApi - The AG Grid API instance
 * @param {string} [label] - Button tooltip text
 * @param {string} [resetLabel] - Reset link text
 */
function initColumnChooser(containerId, gridApi, label, resetLabel) {
    const container = document.getElementById(containerId);
    if (!container) return;
    const wrapper_el = container.closest('.desk-grid-wrapper');
    if (!wrapper_el) return;
    const toolbar = wrapper_el.querySelector('.desk-grid-toolbar-actions') || wrapper_el.querySelector('.desk-grid-toolbar');
    if (!toolbar) return;

    const storageKey = 'desk-grid-cols-' + containerId;

    // Snapshot default order + visibility
    const defaultDefs = gridApi.getColumnDefs().map(col => ({
        id: colId(col),
        visible: !col._defaultHide,
        _defaultIndex: col._defaultIndex
    })).filter(e => e.id);

    function persistState() {
        saveColumnState(storageKey, getColumnState(gridApi));
    }

    // Persist order on column drag
    gridApi.addEventListener('columnMoved', (e) => {
        if (e.finished) persistState();
    });

    const wrapper = document.createElement('div');
    wrapper.className = 'desk-col-chooser';

    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'btn-icon';
    btn.title = label || 'Columns';
    btn.innerHTML = _colChooserSvg; // static SVG, safe

    const panel = document.createElement('div');
    panel.className = 'desk-col-chooser-panel';

    function buildPanel() {
        panel.textContent = '';
        const cols = gridApi.getColumnDefs();
        cols.forEach(col => {
            const id = colId(col);
            if (!id || col.suppressMovable) return;

            const item = document.createElement('label');
            item.className = 'desk-col-chooser-item';

            const cb = document.createElement('input');
            cb.type = 'checkbox';
            cb.checked = !col.hide;
            cb.addEventListener('change', () => {
                col.hide = !cb.checked;
                gridApi.setGridOption('columnDefs', cols);
                persistState();
            });

            const span = document.createElement('span');
            span.textContent = col.headerName || col.field;

            item.appendChild(cb);
            item.appendChild(span);
            panel.appendChild(item);
        });

        // Reset link
        const resetBtn = document.createElement('button');
        resetBtn.type = 'button';
        resetBtn.className = 'desk-col-chooser-reset';
        resetBtn.textContent = resetLabel || 'Reset to default';
        resetBtn.addEventListener('click', (e) => {
            e.stopPropagation();
            localStorage.removeItem(storageKey);
            // Restore default order and visibility
            const cols = gridApi.getColumnDefs();
            defaultDefs.forEach(def => {
                const col = cols.find(c => colId(c) === def.id);
                if (col) col.hide = !def.visible;
            });
            cols.sort((a, b) => ((a._defaultIndex ?? 999) - (b._defaultIndex ?? 999)));
            gridApi.setGridOption('columnDefs', cols);
            buildPanel();
        });
        panel.appendChild(resetBtn);
    }

    btn.addEventListener('click', (e) => {
        e.stopPropagation();
        const isOpen = wrapper.classList.toggle('open');
        if (isOpen) buildPanel();
    });

    document.addEventListener('click', (e) => {
        if (!wrapper.contains(e.target)) {
            wrapper.classList.remove('open');
        }
    });

    wrapper.appendChild(btn);
    wrapper.appendChild(panel);
    toolbar.appendChild(wrapper);
}

/**
 * Posts JSON to a Razor Pages handler.
 * @param {string} url - Handler URL
 * @param {object} data - Request body
 * @param {string} antiforgeryToken - CSRF token
 * @returns {Promise<object>} Response JSON
 */
async function postHandler(url, data, antiforgeryToken) {
    const response = await fetch(url, {
        method: 'POST',
        headers: {
            'Content-Type': 'application/json',
            'RequestVerificationToken': antiforgeryToken
        },
        body: JSON.stringify(data)
    });
    if (!response.ok) {
        const error = await response.json().catch(() => ({ error: `HTTP ${response.status}` }));
        throw new Error(error.error || `HTTP ${response.status}`);
    }
    return response.json();
}
