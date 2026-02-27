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
    const options = defaultGridOptions(gridOptions);
    const gridApi = agGrid.createGrid(container, options);
    return gridApi;
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
