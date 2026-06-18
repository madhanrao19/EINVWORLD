// wwwroot/js/site.js
document.addEventListener('DOMContentLoaded', function () {
    document.getElementById('toggleNavbarBtn').addEventListener('click', toggleNavbar);
});

function toggleNavbar() {
    var navbar = document.getElementById('navbar');
    if (navbar.classList.contains('navbar-horizontal')) {
        navbar.classList.remove('navbar-horizontal');
        navbar.classList.add('navbar-vertical');
    } else {
        navbar.classList.remove('navbar-vertical');
        navbar.classList.add('navbar-horizontal');
    }
}
