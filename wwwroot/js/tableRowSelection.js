document.addEventListener("DOMContentLoaded", function () {
    const selectAllCheckbox = document.getElementById("selectAll");

    // Ensure the <head> element exists before appending styles
    if (document.head) {
        const style = document.createElement("style");
        style.innerHTML = `
            .selected-row {
                background-color: #e0e0e0 !important; /* Light grey highlight */
            }
        `;
        document.head.appendChild(style);
    }

    function updateCheckboxes() {
        const checkboxes = document.querySelectorAll(".invoice-checkbox");
        checkboxes.forEach(checkbox => {
            checkbox.checked = selectAllCheckbox.checked;
            toggleRowHighlight(checkbox.closest("tr"), checkbox.checked);
        });
    }

    function toggleRowHighlight(row, isChecked) {
        if (isChecked) {
            row.classList.add("selected-row");
        } else {
            row.classList.remove("selected-row");
        }
    }

    if (selectAllCheckbox) {
        selectAllCheckbox.addEventListener("click", updateCheckboxes);
    }

    document.addEventListener("change", function (event) {
        if (event.target.classList.contains("invoice-checkbox")) {
            const checkboxes = document.querySelectorAll(".invoice-checkbox");
            selectAllCheckbox.checked = [...checkboxes].every(cb => cb.checked);
            toggleRowHighlight(event.target.closest("tr"), event.target.checked);
        }
    });

    // Add row click functionality
    document.querySelectorAll("#invoiceTable tbody tr").forEach(row => {
        row.addEventListener("click", function (event) {
            // Prevent toggling when clicking inside a dropdown or a checkbox
            if (event.target.closest(".dropdown") || event.target.classList.contains("invoice-checkbox")) return;

            const checkbox = row.querySelector(".invoice-checkbox");
            if (checkbox) {
                checkbox.checked = !checkbox.checked;
                toggleRowHighlight(row, checkbox.checked);

                // Update selectAll checkbox state
                const checkboxes = document.querySelectorAll(".invoice-checkbox");
                selectAllCheckbox.checked = [...checkboxes].every(cb => cb.checked);
            }
        });
    });
});
