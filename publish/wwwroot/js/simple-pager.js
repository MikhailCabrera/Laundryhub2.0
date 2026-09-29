/**
 * Laundry Hub Davao - Simple client-side pager for server-rendered tables.
 * Page size defaults to 10. Preserves existing search/filter by operating on
 * rows not hidden by other filters where possible.
 * Usage: SimplePager.paginate('tbody-id', 'pagination-id', 10);
 * For grouped rows (e.g. order + claim sub-row), mark each group with
 * data-pager-group="id" on consecutive <tr> elements; the pager treats each
 * group as one item.
 */
(function () {
    function groupsOf(tbody) {
        var rows = Array.from(tbody.querySelectorAll(':scope > tr'));
        var groups = [];
        var currentKey = null;
        var current = [];
        rows.forEach(function (r) {
            var key = r.getAttribute('data-pager-group');
            if (key) {
                if (key !== currentKey) {
                    if (current.length) groups.push(current);
                    current = [r];
                    currentKey = key;
                } else {
                    current.push(r);
                }
            } else {
                if (current.length) groups.push(current);
                groups.push([r]);
                current = [];
                currentKey = null;
            }
        });
        if (current.length) groups.push(current);
        return groups;
    }

    function paginate(tbodyId, paginationId, pageSize, opts) {
        var tbody = document.getElementById(tbodyId);
        var container = document.getElementById(paginationId);
        if (!tbody || !container) return null;
        var size = pageSize || 10;
        var state = { page: 1, size: size };

        function visibleGroups() {
            // Respect rows hidden by existing filters (class 'hidden' or display:none),
            // but only at group-leader level.
            return groupsOf(tbody).filter(function (g) {
                var leader = g[0];
                return !leader.classList.contains('hidden') && leader.style.display !== 'none';
            });
        }

        function render() {
            var groups = visibleGroups();
            var total = groups.length;
            var totalPages = Math.max(1, Math.ceil(total / state.size));
            if (state.page > totalPages) state.page = totalPages;
            var start = (state.page - 1) * state.size;
            var end = Math.min(start + state.size, total);
            var show = new Set(groups.slice(start, end).flat());
            groupsOf(tbody).forEach(function (g) {
                var inPage = g.some(function (r) { return show.has(r); });
                var filteredOut = g[0].classList.contains('pager-filtered-out');
                g.forEach(function (r) {
                    r.style.display = (!inPage || filteredOut) ? 'none' : '';
                });
            });
            if (total === 0) { container.innerHTML = ''; return; }
            var nums = '';
            var sPage = Math.max(1, Math.min(state.page - 2, totalPages - 4));
            var ePage = Math.min(totalPages, sPage + 4);
            for (var p = Math.max(1, ePage - 4); p <= ePage; p++) {
                nums += '<button type="button" class="lh-page-btn' + (p === state.page ? ' active' : '') + '" data-sp-page="' + p + '">' + p + '</button>';
            }
            container.innerHTML = '<div class="lh-pagination-wrapper"><div class="lh-pagination-info">Showing <strong>' +
                (total === 0 ? 0 : start + 1) + '</strong> to <strong>' + end + '</strong> of <strong>' + total +
                '</strong> entries</div><div class="lh-pagination-nav"><button type="button" class="lh-page-btn lh-page-prev"' +
                (state.page <= 1 ? ' disabled' : '') + ' data-sp-page="' + (state.page - 1) + '">Previous</button><div class="lh-page-numbers">' +
                nums + '</div><button type="button" class="lh-page-btn lh-page-next"' +
                (state.page >= totalPages ? ' disabled' : '') + ' data-sp-page="' + (state.page + 1) + '">Next</button></div></div>';
            container.querySelectorAll('[data-sp-page]').forEach(function (btn) {
                btn.addEventListener('click', function () {
                    var target = parseInt(btn.getAttribute('data-sp-page'), 10);
                    if (!isNaN(target)) { state.page = target; render(); }
                });
            });
        }

        function refresh() { state.page = 1; render(); }

        render();
        return { render: render, refresh: refresh, state: state };
    }

    window.SimplePager = { paginate: paginate };
})();
