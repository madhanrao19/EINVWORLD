/**
 * Enhanced Table Scroll Behavior
 * Reusable JavaScript for scrollable tables with gradient shadows
 * Usage: Just include this script and add .table-scroll wrapper to your tables
 */

// Initialize enhanced table scroll behavior
function initializeEnhancedTable() {
    const scrollers = document.querySelectorAll('.table-scroll');
    
    scrollers.forEach(scroller => {
        const inner = scroller.querySelector('.inner');
        const hint = scroller.querySelector('.swipe-hint');
        
        if (!inner) return;
        
        function updateShadows() {
            const maxScroll = inner.scrollWidth - inner.clientWidth - 1;
            const isAtLeft = inner.scrollLeft <= 0;
            const isAtRight = inner.scrollLeft >= maxScroll;
            
            scroller.classList.toggle('at-left', isAtLeft);
            scroller.classList.toggle('at-right', isAtRight);
            
            // Hide swipe hint after scrolling
            if (hint && inner.scrollLeft > 20) {
                hint.style.display = 'none';
            }
        }
        
        // Add event listeners
        ['scroll', 'resize'].forEach(event => 
            inner.addEventListener(event, updateShadows, { passive: true })
        );
        
        window.addEventListener('resize', updateShadows, { passive: true });
        
        // Initial update
        updateShadows();
        
        // Auto-hide hint after 5 seconds
        if (hint) {
            setTimeout(() => {
                hint.style.opacity = '0';
                setTimeout(() => {
                    hint.style.display = 'none';
                }, 300);
            }, 5000);
        }
    });
}

// Auto-initialize when DOM is ready
document.addEventListener('DOMContentLoaded', function() {
    initializeEnhancedTable();
});

// Export for manual initialization if needed
window.initializeEnhancedTable = initializeEnhancedTable;