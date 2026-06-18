

//const html = document.getElementById("htmlPage");
//const checkbox = document.getElementById("checkbox");
//checkbox.addEventListener("change", () => {
//    if (checkbox.checked) {
//        html.setAttribute("data-bs-theme", "dark");
//    } else {
//        html.setAttribute("data-bs-theme", "light");
//    }

//});

// Sidebar Toggle
const sidebarToggle = document.querySelector("#sidebar-toggle");
sidebarToggle.addEventListener("click", function () {
    document.querySelector("#sidebar").classList.toggle("collapsed");
});

// Dark Mode Toggle
const html = document.getElementById("htmlPage");
const checkbox = document.getElementById("checkbox");

// Check localStorage to set the theme initially
if (localStorage.getItem("theme") === "dark") {
    html.setAttribute("data-bs-theme", "dark");
    checkbox.checked = true;
} else {
    html.setAttribute("data-bs-theme", "light");
}

// Event listener for the dark mode checkbox
checkbox.addEventListener("change", () => {
    if (checkbox.checked) {
        html.setAttribute("data-bs-theme", "dark");
        localStorage.setItem("theme", "dark");
    } else {
        html.setAttribute("data-bs-theme", "light");
        localStorage.setItem("theme", "light");
    }
});


function clearFilters() {
    const url = new URL(window.location.href);
    url.searchParams.delete("searchName");
    url.searchParams.delete("searchTIN");
    url.searchParams.delete("searchEmail");
    window.location.href = url.toString();
}

function initializeDataTable(selector, options) {
    $(selector).DataTable(options);
}

// Function to initialize DataTable with search enabled
function initDataTableWithSearch() {
    const options = {
        searching: true,
        ordering: true,
        paging: true // Add pagination if needed
    };
    initializeDataTable('#dataTable', options);
    initializeDataTable('#dataTable2', options);

  
}

// Function to initialize DataTable with search disabled
function initDataTableWithoutSearch() {
    const options = {
        searching: false,
        ordering: true,
        paging: true // Add pagination if needed
    };
    initializeDataTable('#dataTable1', options);

}

// Document ready function
$(function () {
    initDataTableWithSearch();
    initDataTableWithoutSearch();
});

function clearFilters() {
    const url = new URL(window.location.href);
    url.searchParams.delete("searchTerm"); // Ensure this matches the input name
    window.location.href = url.toString();
}


