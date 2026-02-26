// Company selector dropdown
function toggleCompanyDropdown() {
    const dropdown = document.getElementById('companyDropdown');
    dropdown?.classList.toggle('open');
    // Close user dropdown if open
    document.getElementById('userDropdown')?.classList.remove('open');
}

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
