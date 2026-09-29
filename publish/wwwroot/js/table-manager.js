/**
 * Laundry Hub Davao - Universal Reusable Table Manager
 * Handles live debounced search, filtering, and page-based pagination (10 items/page by default)
 */
class TableManager {
    /**
     * @param {Object} options
     * @param {string} options.tableBodyId - ID of <tbody> element
     * @param {string} options.paginationContainerId - ID of container element for pagination controls
     * @param {string} options.emptyStateId - ID of empty state element
     * @param {string} [options.searchInputId] - ID of search input field
     * @param {number} [options.pageSize=10] - Items per page
     * @param {Function} options.renderRow - Function returning HTML string for an item
     * @param {Function} [options.searchPredicate] - (item, query) => boolean
     */
    constructor(options) {
        this.tableBody = document.getElementById(options.tableBodyId);
        this.paginationContainer = document.getElementById(options.paginationContainerId);
        this.emptyState = document.getElementById(options.emptyStateId);
        this.searchInput = options.searchInputId ? document.getElementById(options.searchInputId) : null;
        this.pageSize = options.pageSize || 10;
        this.renderRow = options.renderRow;
        this.searchPredicate = options.searchPredicate || ((item, q) => true);

        this.allItems = [];
        this.filteredItems = [];
        this.currentPage = 1;
        this.activeFilter = null; // Custom filter callback (e.g. role or status)

        this.initEvents();
    }

    initEvents() {
        if (this.searchInput) {
            this.searchInput.addEventListener('input', () => {
                this.currentPage = 1;
                this.applyFilterAndRender();
            });
        }
    }

    /**
     * Set/update the data collection
     * @param {Array} items
     */
    setData(items) {
        this.allItems = items || [];
        this.applyFilterAndRender();
    }

    /**
     * Apply custom external filter (e.g. role chips)
     * @param {Function|null} filterFn
     */
    setCustomFilter(filterFn) {
        this.activeFilter = filterFn;
        this.currentPage = 1;
        this.applyFilterAndRender();
    }

    applyFilterAndRender() {
        const query = (this.searchInput ? this.searchInput.value : '').trim().toLowerCase();

        this.filteredItems = this.allItems.filter(item => {
            // Apply custom tab/chip filter if any
            if (this.activeFilter && !this.activeFilter(item)) {
                return false;
            }
            // Apply search query
            if (query && this.searchPredicate) {
                return this.searchPredicate(item, query);
            }
            return true;
        });

        const totalPages = Math.max(1, Math.ceil(this.filteredItems.length / this.pageSize));
        if (this.currentPage > totalPages) {
            this.currentPage = totalPages;
        }

        this.render();
    }

    goToPage(page) {
        const totalPages = Math.max(1, Math.ceil(this.filteredItems.length / this.pageSize));
        if (page < 1 || page > totalPages) return;
        this.currentPage = page;
        this.render();
    }

    render() {
        const totalItems = this.filteredItems.length;
        const totalPages = Math.max(1, Math.ceil(totalItems / this.pageSize));

        if (!this.tableBody) return;

        if (totalItems === 0) {
            this.tableBody.innerHTML = '';
            if (this.emptyState) this.emptyState.style.display = 'flex';
            if (this.paginationContainer) this.paginationContainer.innerHTML = '';
            return;
        }

        if (this.emptyState) this.emptyState.style.display = 'none';

        const startIndex = (this.currentPage - 1) * this.pageSize;
        const endIndex = Math.min(startIndex + this.pageSize, totalItems);
        const pageSlice = this.filteredItems.slice(startIndex, endIndex);

        this.tableBody.innerHTML = pageSlice.map((item, idx) => this.renderRow(item, startIndex + idx)).join('');

        this.renderPagination(startIndex + 1, endIndex, totalItems, totalPages);
    }

    renderPagination(from, to, total, totalPages) {
        if (!this.paginationContainer) return;

        let pageButtons = '';
        
        // Show max 5 numbered pages around current page
        let startPage = Math.max(1, this.currentPage - 2);
        let endPage = Math.min(totalPages, startPage + 4);
        if (endPage - startPage < 4) {
            startPage = Math.max(1, endPage - 4);
        }

        for (let p = startPage; p <= endPage; p++) {
            pageButtons += `
                <button type="button" class="lh-page-btn ${p === this.currentPage ? 'active' : ''}" data-page="${p}">
                    ${p}
                </button>
            `;
        }

        this.paginationContainer.innerHTML = `
            <div class="lh-pagination-wrapper">
                <div class="lh-pagination-info">
                    Showing <strong>${from}</strong> to <strong>${to}</strong> of <strong>${total}</strong> entries
                </div>
                <div class="lh-pagination-nav">
                    <button type="button" class="lh-page-btn lh-page-prev" ${this.currentPage <= 1 ? 'disabled' : ''} data-page="${this.currentPage - 1}">
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="15 18 9 12 15 6"></polyline></svg>
                        Previous
                    </button>
                    <div class="lh-page-numbers">
                        ${pageButtons}
                    </div>
                    <button type="button" class="lh-page-btn lh-page-next" ${this.currentPage >= totalPages ? 'disabled' : ''} data-page="${this.currentPage + 1}">
                        Next
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><polyline points="9 18 15 12 9 6"></polyline></svg>
                    </button>
                </div>
            </div>
        `;

        // Attach click handlers to pagination buttons
        this.paginationContainer.querySelectorAll('[data-page]').forEach(btn => {
            btn.addEventListener('click', (e) => {
                const targetPage = parseInt(e.currentTarget.getAttribute('data-page'), 10);
                if (!isNaN(targetPage)) {
                    this.goToPage(targetPage);
                }
            });
        });
    }
}

// Export for usage
window.TableManager = TableManager;
