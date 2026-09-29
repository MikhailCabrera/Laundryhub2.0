/**
 * Laundry Hub Davao - Shared Order Timeline
 *
 * One stage-derivation + renderer pair for every surface. The stage list is
 * parameterized per surface:
 *   legs       - admin pickup / delivery / tracking / CRM legs (read-only chips)
 *   processing - rider read-only view of its own order's stages
 *   journey    - customer 7-step tracking journey
 *
 * Renders the single `.lh-timeline*` class family declared in ~/css/site.css.
 * No DOM access at load time, so it can also be executed head-less for proofs.
 */
(function (root, factory) {
    var api = factory();
    if (typeof window !== 'undefined') window.OrderTimeline = api;
    if (typeof module !== 'undefined' && module.exports) module.exports = api;
})(typeof self !== 'undefined' ? self : this, function () {
    'use strict';

    /** OrderStatus ordinals — mirrors LaundryHub2._0.Models.OrderStatus. */
    var STATUS_ORDINAL = {
        pending: 0,
        riderassigned: 1,
        pickedup: 2,
        intransittoshop: 3,
        weighing: 4,
        weightconfirmed: 5,
        washing: 6,
        drying: 7,
        awaitingpayment: 8,
        paymentconfirmed: 9,
        readyfordelivery: 10,
        outfordelivery: 11,
        delivered: 12,
        cancelled: 13,
        abandoned: 14,
        deliveryattemptfailed: 15
    };

    /** Statuses that end the journey without completing any progress step. */
    var TERMINAL_FAILURE = { cancelled: true, abandoned: true, deliveryattemptfailed: true };

    var TERMINAL_FAILURE_NAMES = { cancelled: 'Cancelled', abandoned: 'Abandoned', deliveryattemptfailed: 'DeliveryAttemptFailed' };

    /** Which stage a status currently sits on (shared by legs and processing). */
    var STATUS_STAGE_KEY = {
        pending: 'placed',
        riderassigned: 'pickup_assigned',
        pickedup: 'picked_up',
        intransittoshop: 'in_transit',
        atshop: 'in_transit',
        weighing: 'in_transit',
        weightconfirmed: 'weighed',
        washing: 'washing',
        drying: 'drying',
        awaitingpayment: 'drying',
        paymentconfirmed: 'ready',
        readyfordelivery: 'ready',
        outfordelivery: 'out_for_delivery',
        deliveryattemptfailed: 'out_for_delivery',
        delivered: 'delivered'
    };

    /** Timestamp-driven stage lists. `at` is the order field that closes the stage. */
    var STAGE_LISTS = {
        legs: [
            { key: 'placed', label: 'Order placed', at: 'createdAt' },
            { key: 'pickup_assigned', label: 'Pickup rider assigned', at: 'riderAssignedAt' },
            { key: 'picked_up', label: 'Picked up', at: 'pickedUpAt' },
            { key: 'in_transit', label: 'In transit to shop', at: null },
            { key: 'weighed', label: 'Weight confirmed', at: 'weightConfirmedAt' },
            { key: 'washing', label: 'Washing', at: 'washingStartedAt' },
            { key: 'drying', label: 'Drying', at: 'dryingStartedAt' },
            { key: 'ready', label: 'Ready for delivery', at: 'readyNotifiedAt' },
            { key: 'out_for_delivery', label: 'Out for delivery', at: 'deliveryAssignedAt' },
            { key: 'delivered', label: 'Delivered', at: 'deliveredAt' }
        ],
        processing: [
            { key: 'pickup_assigned', label: 'Pickup rider assigned', at: 'riderAssignedAt' },
            { key: 'picked_up', label: 'Picked up', at: 'pickedUpAt' },
            { key: 'in_transit', label: 'In transit to shop', at: null },
            { key: 'weighed', label: 'Weight confirmed', at: 'weightConfirmedAt' },
            { key: 'washing', label: 'Washing', at: 'washingStartedAt' },
            { key: 'drying', label: 'Drying', at: 'dryingStartedAt' },
            { key: 'ready', label: 'Ready for delivery', at: 'readyNotifiedAt' },
            { key: 'out_for_delivery', label: 'Out for delivery', at: 'deliveryAssignedAt' },
            { key: 'delivered', label: 'Delivered', at: 'deliveredAt' }
        ]
    };

    /** Journey step -> status ordinal that completes it. Step 1 is always booked. */
    var JOURNEY_STEP_STATUS = { 2: 'riderassigned', 3: 'pickedup', 4: 'weightconfirmed', 5: 'washing', 6: 'drying' };

    var JOURNEY_COPY = {
        1: { title: '1. Pickup Request Booked' },
        2: { title: '2. Rider Assigned' },
        3: { title: '3. Collected by Rider', desc: 'Clothes securely loaded for transport to our central Davao wash facility.' },
        4: { title: '4. Exact Scale Weighing & Photo Verified' },
        5: { title: '5. Washing & Fabric Care', desc: 'Deep cleaning using premium eco-friendly detergents and sanitizers.' },
        6: { title: '6. Temperature-Controlled Drying & Folding', desc: 'Gentle tumble dry, crisp steam iron (if selected), and neat packaging.' },
        7: { title: '7. Out for Delivery & Returned Fresh' }
    };

    function escapeHtml(v) {
        return String(v == null ? '' : v)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function clean(v) {
        return (v === null || v === undefined || v === '') ? null : v;
    }

    function statusKey(status) {
        return String(status == null ? '' : status).toLowerCase();
    }

    function parts(value, opts) {
        var settings = { timeZone: 'Asia/Manila' };
        for (var k in opts) if (Object.prototype.hasOwnProperty.call(opts, k)) settings[k] = opts[k];
        var out = {};
        new Intl.DateTimeFormat('en-US', settings).formatToParts(new Date(value)).forEach(function (p) {
            out[p.type] = p.value;
        });
        return out;
    }

    /** Customer-facing timestamp, Asia/Manila — matches the Razor ManilaTime(). */
    function manilaDateTime(value) {
        if (clean(value) === null) return '—';
        var d = new Date(value);
        if (Number.isNaN(d.getTime())) return '—';
        var p = parts(d, { year: 'numeric', month: 'long', day: '2-digit', hour: '2-digit', minute: '2-digit', hour12: true });
        return p.month + ' ' + p.day + ', ' + p.year + ' ' + p.hour + ':' + p.minute + ' ' + p.dayPeriod;
    }

    /** Date-only value (yyyy-MM-dd) rendered as "MMM dd, yyyy" with no tz shift. */
    function manilaDateOnly(value) {
        if (clean(value) === null) return '—';
        var bits = String(value).split('-');
        if (bits.length !== 3) return manilaDateTime(value);
        var d = new Date(Date.UTC(Number(bits[0]), Number(bits[1]) - 1, Number(bits[2])));
        if (Number.isNaN(d.getTime())) return '—';
        var p = new Intl.DateTimeFormat('en-US', { timeZone: 'UTC', month: 'short', day: '2-digit', year: 'numeric' }).formatToParts(d);
        var map = {};
        p.forEach(function (x) { map[x.type] = x.value; });
        return map.month + ' ' + map.day + ', ' + map.year;
    }

    /** Admin leg chips: locale short form, or the em dash / in-progress markers. */
    function legTime(at, state) {
        if (clean(at) !== null) {
            var d = new Date(at);
            if (Number.isNaN(d.getTime())) return '—';
            return d.toLocaleString('en-PH', { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
        }
        return state === 'current' ? 'in progress' : '—';
    }

    function money(value) {
        if (value === null || value === undefined || value === '') return null;
        var n = Number(value);
        if (Number.isNaN(n)) return null;
        return n.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }

    /**
     * Stage derivation for the timestamp-driven lists (legs, processing).
     * A stage is done once its timestamp exists, or once a later stage is the
     * current one; the current status' stage is current; everything else is todo.
     */
    function deriveStages(order, surface) {
        var defs = STAGE_LISTS[surface];
        if (!defs) throw new Error('OrderTimeline: unknown surface "' + surface + '"');
        var stages = defs.map(function (d) {
            return { key: d.key, label: d.label, at: clean(order == null ? null : order[d.at]), state: 'todo' };
        });
        var currentKey = STATUS_STAGE_KEY[statusKey(order && order.status)];
        var statusIdx = -1;
        if (currentKey) {
            for (var i = 0; i < stages.length; i++) {
                if (stages[i].key === currentKey) { statusIdx = i; break; }
            }
        }
        stages.forEach(function (s, i) {
            s.state = (s.at !== null || (statusIdx >= 0 && i < statusIdx)) ? 'done' : (i === statusIdx ? 'current' : 'todo');
        });
        return stages;
    }

    function journeyStepDone(statusKeyLower, step) {
        if (TERMINAL_FAILURE[statusKeyLower]) return false;
        var threshold = JOURNEY_STEP_STATUS[step];
        if (!threshold) return false;
        var s = STATUS_ORDINAL[statusKeyLower];
        return typeof s === 'number' && s >= STATUS_ORDINAL[threshold];
    }

    function riderName(r) {
        return r && r.name ? String(r.name) : null;
    }

    function journeyDescStep2(order) {
        var r = order.pickupRider;
        return r ? 'Rider ' + escapeHtml(riderName(r)) + (r.phone ? ' (' + escapeHtml(r.phone) + ')' : '') : 'Awaiting shop rider dispatch';
    }

    function journeyDescStep4(order) {
        var desc = (order.weightKg !== null && order.weightKg !== undefined && order.weightKg !== '')
            ? 'Verified weight: ' + order.weightKg + ' kg' + (money(order.totalAmount) !== null ? ' — Total: ₱' + money(order.totalAmount) : '')
            : 'Awaiting arrival and scale weighing';
        if (order.weightPhotoPath) {
            desc += '<p class="lh-timeline-note"><a class="lh-timeline-link" href="' + escapeHtml(order.weightPhotoPath) + '" target="_blank" rel="noopener">View weight photo</a></p>';
        }
        return desc;
    }

    function journeyDescStep7(order, status) {        if (status === 'delivered') {
            var lines = ['<div>Delivered successfully to your doorstep.</div>'];
            if (riderName(order.deliveryRider)) lines.push('<div>Delivered by ' + escapeHtml(riderName(order.deliveryRider)) + '.</div>');
            if (clean(order.deliveredAt) !== null) lines.push('<div>' + escapeHtml(manilaDateTime(order.deliveredAt)) + '</div>');
            if (order.deliveryPhotoPath) {
                lines.push('<a class="lh-timeline-link" href="' + escapeHtml(order.deliveryPhotoPath) + '" target="_blank" rel="noopener">View proof of delivery</a>');
            }
            return lines.join('');
        }
        if (status === 'outfordelivery') {
            var out = ['<div>Your laundry is on the way to your doorstep.</div>'];
            var dr = order.deliveryRider;
            if (riderName(dr)) {
                out.push('<div>Delivery rider: ' + escapeHtml(riderName(dr)) + (dr.phone ? ' (' + escapeHtml(dr.phone) + ')' : '') + '</div>');
            }
            return out.join('');
        }
        if (status === 'readyfordelivery') return '<div>Payment confirmed. Waiting for the shop to assign a delivery rider.</div>';
        return '<div>Delivery will be scheduled after payment is confirmed.</div>';
    }

    /**
     * Stage derivation for the customer journey (status-driven, 7 steps).
     */
    function deriveJourney(order) {
        var status = statusKey(order && order.status);
        // Selected-services flags (default true for backward-compat payloads).
        var needsWash = !order || order.requiresWashing === undefined || order.requiresWashing === null ? true : !!order.requiresWashing;
        var needsDry = !order || order.requiresDrying === undefined || order.requiresDrying === null ? true : !!order.requiresDrying;
        var steps = [];
        for (var n = 1; n <= 7; n++) {
            var state = 'todo';
            if (n === 1) state = 'done';
            else if (n === 7) {
                if (status === 'delivered') state = 'done';
                else if (status === 'outfordelivery') state = 'current';
            } else if (journeyStepDone(status, n)) state = 'done';

            var desc = '';
            if (n === 1) {
                desc = 'Scheduled for ' + escapeHtml(manilaDateOnly(order.preferredPickupDate))
                    + ' (' + escapeHtml(order.preferredPickupTime == null ? '' : order.preferredPickupTime) + ')';
            } else if (n === 2) {
                desc = journeyDescStep2(order);
            } else if (n === 3) {
                desc = escapeHtml(JOURNEY_COPY[3].desc);
            } else if (n === 4) {
                desc = journeyDescStep4(order);
            } else if (n === 5) {
                if (!needsWash) {
                    state = 'done';
                    desc = 'Not part of your selected services.';
                } else {
                    // Mark current washing stage when order is actively washing.
                    if (state === 'todo' && (status === 'washing' || status === 'paymentconfirmed')) state = status === 'washing' ? 'current' : 'todo';
                    desc = escapeHtml(JOURNEY_COPY[5].desc);
                }
            } else if (n === 6) {
                if (!needsDry) {
                    state = 'done';
                    desc = 'Not part of your selected services.';
                } else {
                    if (state === 'todo' && status === 'drying') state = 'current';
                    desc = escapeHtml(JOURNEY_COPY[6].desc);
                }
            } else {
                desc = journeyDescStep7(order, status);
            }

            var title = JOURNEY_COPY[n].title;
            if ((n === 5 && !needsWash) || (n === 6 && !needsDry)) title += ' (not selected)';

            steps.push({
                key: 'journey_' + n,
                n: n,
                title: title,
                state: state,
                desc: desc,
                marker: state === 'done' ? '✓' : String(n)
            });
        }
        return { status: status, steps: steps, terminalFailure: !!TERMINAL_FAILURE[status], terminalName: TERMINAL_FAILURE_NAMES[status] };
    }

    function stepClass(state, extra) {
        return 'lh-timeline-step lh-timeline-step--' + state + (extra ? ' ' + extra : '');
    }

    function renderChips(stages, options) {
        var opts = options || {};
        var html = '<ol class="lh-timeline lh-timeline--chips">';
        stages.forEach(function (s) {
            var extra = s.mine ? 'lh-timeline-step--mine' : '';
            html += '<li class="' + stepClass(s.state, extra) + '">'
                + '<span class="lh-timeline-dot"></span>'
                + '<span class="lh-timeline-label">' + escapeHtml(s.label) + '</span>'
                + '<span class="lh-timeline-time">' + escapeHtml(opts.timeFor ? opts.timeFor(s) : legTime(s.at, s.state)) + '</span>'
                + '</li>';
        });
        return html + '</ol>';
    }

    function renderJourney(journey, options) {
        var opts = options || {};
        var html = '<div class="lh-timeline lh-timeline--journey">';
        journey.steps.forEach(function (s) {
            if (opts.terminalAfterFirst && journey.terminalFailure && s.n === 2) html += renderTerminal(journey, opts.orderNumber);
            var extra = s.mine ? 'lh-timeline-step--mine' : '';
            var descTag = /^<(div|p|ul|ol|table)\b/i.test(s.desc) ? 'div' : 'p';
            html += '<div class="' + stepClass(s.state, extra) + '">'
                + '<div class="lh-timeline-dot">' + escapeHtml(s.marker) + '</div>'
                + '<h4 class="lh-timeline-title">' + escapeHtml(s.title) + '</h4>'
                + '<' + descTag + ' class="lh-timeline-desc">' + s.desc + '</' + descTag + '>'
                + '</div>';
        });
        return html + '</div>';
    }

    function renderTerminal(journey, orderNumber) {
        return '<div class="lh-timeline-terminal">'
            + '<div class="lh-timeline-terminal-title">This order ended as ' + escapeHtml(journey.terminalName) + '</div>'
            + '<div class="lh-timeline-terminal-text">No further laundry steps apply. Contact support if you need help with order ' + escapeHtml(orderNumber) + '.</div>'
            + '</div>';
    }

    function markOwnLeg(stages, leg) {
        if (leg !== 'pickup' && leg !== 'delivery') return stages;
        var ownKey = leg === 'pickup' ? 'pickup_assigned' : 'out_for_delivery';
        stages.forEach(function (s) { if (s.key === ownKey) s.mine = true; });
        return stages;
    }

    /**
     * Render one surface of one order.
     * @param {Object} order
     * @param {string} surface - 'legs' | 'processing' | 'journey'
     * @param {Object} [options] - { leg, terminalAfterFirst, orderNumber }
     */
    function render(order, surface, options) {
        var opts = options || {};
        if (surface === 'processing') {
            return renderChips(markOwnLeg(deriveStages(order, 'processing'), opts.leg), {
                timeFor: function (s) { return legTime(s.at, s.state); }
            });
        }
        if (surface === 'journey') {
            return renderJourney(deriveJourney(order), {
                terminalAfterFirst: true,
                orderNumber: (opts.orderNumber || (order && order.orderNumber))
            });
        }
        return renderChips(deriveStages(order, 'legs'), opts);
    }

    /** Render every [data-timeline-surface] mount found under `scope`. */
    function mountAll(scope) {
        var doc = scope || (typeof document !== 'undefined' ? document : null);
        if (!doc) return;
        var mounts = doc.querySelectorAll('[data-timeline-surface]');
        for (var i = 0; i < mounts.length; i++) {
            var el = mounts[i];
            var payloadId = el.getAttribute('data-timeline-orders');
            var data = payloadId ? readJson(doc.getElementById(payloadId)) : null;
            if (!data) continue;
            var order = data[el.getAttribute('data-timeline-index')];
            if (!order) continue;
            el.innerHTML = render(order, el.getAttribute('data-timeline-surface'), {
                leg: el.getAttribute('data-timeline-leg') || null,
                orderNumber: order.orderNumber
            });
        }
    }

    function readJson(el) {
        if (!el) return null;
        try { return JSON.parse(el.textContent); } catch (e) { return null; }
    }

    return {
        STATUS_ORDINAL: STATUS_ORDINAL,
        TERMINAL_FAILURE: TERMINAL_FAILURE,
        STAGE_LISTS: STAGE_LISTS,
        stages: deriveStages,
        journey: deriveJourney,
        render: render,
        mountAll: mountAll,
        format: { manilaDateTime: manilaDateTime, manilaDateOnly: manilaDateOnly, legTime: legTime },
        escapeHtml: escapeHtml
    };
});
