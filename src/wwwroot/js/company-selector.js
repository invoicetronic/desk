// Company selector dropdown
function toggleCompanyDropdown() {
    const dropdown = document.getElementById('companyDropdown');
    const wasOpen = dropdown?.classList.contains('open');
    dropdown?.classList.toggle('open');
    // Close user dropdown if open
    document.getElementById('userDropdown')?.classList.remove('open');

    if (!wasOpen) {
        const search = document.getElementById('companySearch');
        if (search) {
            search.value = '';
            filterCompanies('');
            setHighlight(-1);
            setTimeout(() => search.focus(), 0);
        }
    }
}

// Filter company list by search term
function filterCompanies(term) {
    const links = document.querySelectorAll('#companyList a');
    const lower = term.toLowerCase();
    links.forEach(a => {
        if (a.hasAttribute('data-all')) return; // always show "All companies"
        a.classList.toggle('hidden', !a.textContent.toLowerCase().includes(lower));
    });
}

// Keyboard navigation state
var _highlightIndex = -1;

function getVisibleItems() {
    return Array.from(document.querySelectorAll('#companyList a:not(.hidden)'));
}

function setHighlight(index) {
    const items = getVisibleItems();
    // Remove previous highlight
    items.forEach(a => a.classList.remove('highlight'));
    _highlightIndex = Math.max(-1, Math.min(index, items.length - 1));
    if (_highlightIndex >= 0 && items[_highlightIndex]) {
        items[_highlightIndex].classList.add('highlight');
        items[_highlightIndex].scrollIntoView({ block: 'nearest' });
    }
}

// Wire up search input (no DOMContentLoaded — script is at bottom of body)
(function () {
    const search = document.getElementById('companySearch');
    if (!search) return;

    search.addEventListener('input', function () {
        filterCompanies(search.value);
        setHighlight(-1);
    });

    search.addEventListener('keydown', function (e) {
        if (e.key === 'ArrowDown') {
            e.preventDefault();
            setHighlight(_highlightIndex + 1);
        } else if (e.key === 'ArrowUp') {
            e.preventDefault();
            setHighlight(_highlightIndex - 1);
        } else if (e.key === 'Enter') {
            e.preventDefault();
            var items = getVisibleItems();
            var target = _highlightIndex >= 0 ? items[_highlightIndex] : items.find(a => !a.hasAttribute('data-all'));
            if (target) target.click();
        } else if (e.key === 'Escape') {
            document.getElementById('companyDropdown')?.classList.remove('open');
        }
    });

    // Prevent dropdown from closing when interacting with search
    search.addEventListener('click', e => e.stopPropagation());
})();

// User menu dropdown
function toggleUserDropdown() {
    const dropdown = document.getElementById('userDropdown');
    dropdown?.classList.toggle('open');
    // Close company dropdown if open
    document.getElementById('companyDropdown')?.classList.remove('open');
}

// Close dropdowns on outside click
document.addEventListener('click', function (e) {
    const companySelector = document.getElementById('companySelector');
    const userMenu = document.getElementById('userMenu');

    if (companySelector && !companySelector.contains(e.target)) {
        document.getElementById('companyDropdown')?.classList.remove('open');
    }
    if (userMenu && !userMenu.contains(e.target)) {
        document.getElementById('userDropdown')?.classList.remove('open');
    }
});
