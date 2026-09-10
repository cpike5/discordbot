/* ==========================================================================
   Report base script — v1
   Optional. A report with no interactive parts can drop this entirely.
   Everything is opt-in via markup: no element, no behaviour.
   Inline inside <script> just before </body>.
   ========================================================================== */
(function () {
  "use strict";

  /* ---------- tiny helpers ---------- */
  const $  = (sel, root) => (root || document).querySelector(sel);
  const $$ = (sel, root) => Array.from((root || document).querySelectorAll(sel));
  const esc = (s) => String(s).replace(/[&<>"']/g, (c) =>
    ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));

  /* ---------- 1. THEME ----------------------------------------------------
     Needs: <button id="theme-toggle">. Falls back to system preference.
     Storage is best-effort — the toggle still works if it is unavailable.
  ------------------------------------------------------------------------ */
  function initTheme() {
    const btn = $("#theme-toggle");
    const root = document.documentElement;
    let saved = null;
    try { saved = localStorage.getItem("report-theme"); } catch (e) { /* ignore */ }
    if (saved === "dark" || saved === "light") root.setAttribute("data-theme", saved);

    const isDark = () =>
      root.getAttribute("data-theme") === "dark" ||
      (!root.hasAttribute("data-theme") && matchMedia("(prefers-color-scheme: dark)").matches);

    const paint = () => { if (btn) btn.textContent = isDark() ? "Light" : "Dark"; };
    paint();
    if (!btn) return;

    btn.addEventListener("click", () => {
      const next = isDark() ? "light" : "dark";
      root.setAttribute("data-theme", next);
      try { localStorage.setItem("report-theme", next); } catch (e) { /* ignore */ }
      paint();
    });
  }

  /* ---------- 2. TOOLTIPS -------------------------------------------------
     Needs: <div id="tip"></div> in the body, and any element carrying
     data-tip="…". Values may contain markup you generated yourself.
     For dynamic elements, call window.Report.bindTip(node, html).
  ------------------------------------------------------------------------ */
  let tipEl = null;
  function bindTip(node, html) {
    if (!tipEl) return;
    node.addEventListener("mouseenter", () => { tipEl.innerHTML = html; tipEl.style.opacity = "1"; });
    node.addEventListener("mousemove", (e) => {
      const pad = 14;
      const r = tipEl.getBoundingClientRect();
      let x = e.clientX + pad, y = e.clientY + pad;
      if (x + r.width  > innerWidth  - 8) x = e.clientX - r.width  - pad;
      if (y + r.height > innerHeight - 8) y = e.clientY - r.height - pad;
      tipEl.style.left = x + "px";
      tipEl.style.top  = y + "px";
    });
    node.addEventListener("mouseleave", () => { tipEl.style.opacity = "0"; });
  }
  function initTips() {
    tipEl = $("#tip");
    if (!tipEl) return;
    $$("[data-tip]").forEach((n) => bindTip(n, n.getAttribute("data-tip")));
  }

  /* ---------- 3. SORTABLE TABLES -----------------------------------------
     Needs: <th data-sort="text"> or <th data-sort="num"> in a <thead>.
     Sorts <tbody> rows only; <tfoot> totals stay put.
  ------------------------------------------------------------------------ */
  function initSort() {
    $$("table thead th[data-sort]").forEach((th) => {
      th.addEventListener("click", () => {
        const table = th.closest("table");
        const tbody = table.tBodies[0];
        if (!tbody) return;
        const idx  = Array.from(th.parentNode.children).indexOf(th);
        const kind = th.getAttribute("data-sort");
        const asc  = th.getAttribute("aria-sort") !== "ascending";

        $$("th[data-sort]", table).forEach((o) => o.removeAttribute("aria-sort"));
        th.setAttribute("aria-sort", asc ? "ascending" : "descending");

        const val = (row) => {
          const cell = row.children[idx];
          const raw  = cell ? cell.textContent.trim() : "";
          if (kind !== "num") return raw.toLowerCase();
          const n = parseFloat(raw.replace(/[^0-9.\-]/g, ""));
          return isNaN(n) ? -Infinity : n;
        };
        Array.from(tbody.rows)
          .sort((a, b) => { const x = val(a), y = val(b); return (x > y ? 1 : x < y ? -1 : 0) * (asc ? 1 : -1); })
          .forEach((r) => tbody.appendChild(r));
      });
    });
  }

  /* ---------- 4. SECTION HIGHLIGHT ---------------------------------------
     Needs: .agenda a[href="#id"] pointing at h2[id] sections.
     Marks the agenda card for whichever section is currently on screen.
  ------------------------------------------------------------------------ */
  function initScrollspy() {
    const links = $$('.agenda a[href^="#"]');
    if (!links.length || !("IntersectionObserver" in window)) return;
    const map = new Map();
    links.forEach((a) => {
      const t = document.getElementById(decodeURIComponent(a.hash.slice(1)));
      if (t) map.set(t, a);
    });
    const io = new IntersectionObserver((entries) => {
      entries.forEach((en) => {
        const a = map.get(en.target);
        if (a && en.isIntersecting) {
          links.forEach((l) => (l.style.borderLeftColor = ""));
          a.style.borderLeftColor = "var(--c0)";
        }
      });
    }, { rootMargin: "-10% 0px -80% 0px" });
    map.forEach((_a, target) => io.observe(target));
  }

  /* ---------- 5. GENERATED-ON STAMP --------------------------------------
     Needs: any element with data-now. Optional; a hard-coded date is fine
     and is usually better for a document that gets archived.
  ------------------------------------------------------------------------ */
  function initStamp() {
    $$("[data-now]").forEach((n) => {
      n.textContent = new Date().toLocaleDateString(undefined,
        { year: "numeric", month: "long", day: "numeric" });
    });
  }

  /* ---------- 6. WATERFALL CHART -----------------------------------------
     Report.waterfall(hostSelectorOrNode, steps, opts)

     steps: array, walked left to right, of either
       { label: "Claim UX",        delta: 12 }   a movement
       { label: "Post-audit",      total: true } a bar drawn from zero to the
                                                 running value so far
     opts:
       start    number   opening value the first delta applies to (default 0)
       unit     string   appended to every figure, e.g. " h"
       polarity "cost"   a positive delta is bad  → red   (default)
                "value"  a positive delta is good → green
       height   number   SVG height in user units (default 400)
       ticks    number   approximate y-axis tick count (default 5)
       note     fn(step) extra line of tooltip text

     Renders an <svg> into the host. Bars carry tooltips when #tip exists.
  ------------------------------------------------------------------------ */
  function waterfall(host, steps, opts) {
    const el = typeof host === "string" ? $(host) : host;
    if (!el || !Array.isArray(steps) || !steps.length) return;
    opts = opts || {};

    const W = 900, H = opts.height || 400;
    const M = { t: 28, r: 14, b: 78, l: 62 };
    const unit = opts.unit || "";
    const costly = opts.polarity !== "value";
    const nTicks = opts.ticks || 5;

    const round = (v) => Math.round(v * 100) / 100;
    const bare = (v) => round(Math.abs(v)).toLocaleString() + unit;
    const fmtNum = (v) => (v < 0 ? "\u2212" : "") + bare(v);          /* totals and levels */
    const fmtDelta = (v) => (v > 0 ? "+" : v < 0 ? "\u2212" : "") + bare(v); /* movements */

    /* walk the steps into positioned bars */
    let run = opts.start || 0;
    const bars = steps.map((s) => {
      if (s.total || s.subtotal) {
        return { label: s.label, kind: "total", lo: Math.min(0, run), hi: Math.max(0, run), value: run, src: s };
      }
      const from = run;
      run = round(run + s.delta);
      return {
        label: s.label,
        kind: s.delta >= 0 ? "up" : "down",
        lo: Math.min(from, run), hi: Math.max(from, run),
        value: s.delta, end: run, src: s
      };
    });

    /* y scale, snapped to round ticks */
    let lo = Math.min(0, ...bars.map((b) => b.lo));
    let hi = Math.max(0, ...bars.map((b) => b.hi));
    if (hi === lo) hi = lo + 1;
    const rawStep = (hi - lo) / nTicks;
    const pow = Math.pow(10, Math.floor(Math.log10(rawStep) || 0));
    const mult = rawStep / pow;
    const step = (mult <= 1 ? 1 : mult <= 2 ? 2 : mult <= 2.5 ? 2.5 : mult <= 5 ? 5 : 10) * pow;
    lo = Math.floor(lo / step) * step;
    hi = Math.ceil(hi / step) * step;

    const plotH = H - M.t - M.b;
    const plotW = W - M.l - M.r;
    const y = (v) => M.t + ((hi - v) / (hi - lo)) * plotH;
    const band = plotW / bars.length;
    const bw = Math.min(64, band * 0.6);
    const cx = (i) => M.l + band * i + band / 2;

    /* label wrapping — at most three lines */
    const wrap = (text, max) => {
      const out = [];
      let line = "";
      String(text).split(/\s+/).forEach((w) => {
        if (!line.length) line = w;
        else if ((line + " " + w).length <= max) line += " " + w;
        else { out.push(line); line = w; }
      });
      if (line) out.push(line);
      if (out.length > 3) { out.length = 3; out[2] = out[2].slice(0, max - 1) + "\u2026"; }
      return out;
    };

    const p = [];
    p.push('<svg viewBox="0 0 ' + W + " " + H + '" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="' +
      esc(opts.label || "Waterfall chart") + '">');

    /* gridlines + y ticks */
    for (let v = lo; v <= hi + 1e-9; v += step) {
      const yy = y(v).toFixed(1);
      p.push('<line class="' + (v === 0 ? "wf-zero" : "wf-grid") + '" x1="' + M.l + '" x2="' + (W - M.r) +
        '" y1="' + yy + '" y2="' + yy + '"/>');
      p.push('<text class="wf-tick" x="' + (M.l - 10) + '" y="' + yy + '" text-anchor="end" dominant-baseline="middle">' +
        esc(round(v).toLocaleString()) + "</text>");
    }

    /* connectors between consecutive bars */
    bars.forEach((b, i) => {
      const next = bars[i + 1];
      if (!next) return;
      const yy = y(b.kind === "total" ? b.value : b.end).toFixed(1);
      p.push('<line class="wf-conn" x1="' + (cx(i) + bw / 2) + '" x2="' + (cx(i + 1) - bw / 2) +
        '" y1="' + yy + '" y2="' + yy + '"/>');
    });

    /* bars, value labels, category labels */
    bars.forEach((b, i) => {
      let cls = b.kind === "total" ? "wf-total" : b.kind === "up" ? "wf-up" : "wf-down";
      if (!costly && b.kind !== "total") cls = b.kind === "up" ? "wf-down" : "wf-up";

      const top = y(b.hi);
      const h = Math.max(2, y(b.lo) - y(b.hi));
      const x = cx(i) - bw / 2;

      const shown = b.kind === "total" ? fmtNum(b.value) : fmtDelta(b.value);
      const tipRows =
        '<span class="t">' + esc(b.label) + "</span>" +
        '<span class="r">' + (b.kind === "total"
          ? "Running total <b>" + esc(fmtNum(b.value)) + "</b>"
          : "Moves the total by <b>" + esc(fmtDelta(b.value)) + "</b> to <b>" + esc(fmtNum(b.end)) + "</b>") +
        "</span>" +
        (opts.note && opts.note(b.src) ? '<span class="r">' + opts.note(b.src) + "</span>" : "");

      p.push('<rect class="wf-bar ' + cls + '" x="' + x.toFixed(1) + '" y="' + top.toFixed(1) +
        '" width="' + bw.toFixed(1) + '" height="' + h.toFixed(1) + '" rx="3" data-tip="' +
        esc(tipRows) + '"><title>' + esc(b.label + " — " + shown) + "</title></rect>");

      p.push('<text class="wf-val" x="' + cx(i) + '" y="' + (top - 7).toFixed(1) + '" text-anchor="middle">' +
        esc(shown) + "</text>");

      wrap(b.label, Math.max(10, Math.floor(band / 6))).forEach((line, li) => {
        p.push('<text class="wf-lab" x="' + cx(i) + '" y="' + (H - M.b + 20 + li * 13) +
          '" text-anchor="middle">' + esc(line) + "</text>");
      });
    });

    p.push("</svg>");
    el.classList.add("wf");
    el.innerHTML = p.join("");
    $$("[data-tip]", el).forEach((n) => bindTip(n, n.getAttribute("data-tip")));
  }

  /* ---------- 7. GANTT CHART ----------------------------------------------
     Report.gantt(hostSelectorOrNode, rows, opts)

     rows: array, top to bottom, of
       { group: "Gateway" }                          a heading row
       { label, from, to, cat, ids, owner, tip }     a bar
       { label, at, cat, tip, milestone: true }      a diamond marker

     from / to / at accept either an ISO date string ("2026-09-01") or a
     number (units since the project start, e.g. week 3.5). Do not mix the
     two in one chart.

     opts:
       cats     { KEY: { label, c } }  category → legend text + colour 0-5
       from,to  domain bounds; defaults to the extent of the data
       unit     "Week" etc. Label for numeric axes. Ignored for dates.
       today    a date or number to mark with a vertical rule
       bands    [{ from, to, label }]  shaded regions behind the bars
       marks    [{ at, label }]        callouts in the flag lane
       lines    [{ at }]               dashed vertical rules
       caption  text above the label column
       legend   false to suppress the legend

     Bars are labelled inside when they are wide enough, outside when not.
  ------------------------------------------------------------------------ */
  function gantt(host, rows, opts) {
    const el = typeof host === "string" ? $(host) : host;
    if (!el || !Array.isArray(rows) || !rows.length) return;
    opts = opts || {};

    const cats = opts.cats || {};
    const LABEL_PX = 250, MIN_PX = 780;
    const trackPx = MIN_PX - LABEL_PX;

    /* ---- scale: dates or plain numbers ---- */
    const isDate = (v) => typeof v === "string";
    const toNum = (v) => (isDate(v) ? Date.parse(v) : v);
    const dated = rows.some((r) => isDate(r.from) || isDate(r.at));

    const vals = [];
    rows.forEach((r) => {
      if (r.from != null) vals.push(toNum(r.from));
      if (r.to   != null) vals.push(toNum(r.to));
      if (r.at   != null) vals.push(toNum(r.at));
    });
    (opts.bands || []).forEach((b) => { vals.push(toNum(b.from)); vals.push(toNum(b.to)); });
    let d0 = opts.from != null ? toNum(opts.from) : Math.min.apply(null, vals);
    let d1 = opts.to   != null ? toNum(opts.to)   : Math.max.apply(null, vals);
    if (d1 <= d0) d1 = d0 + 1;
    const pct = (v) => ((toNum(v) - d0) / (d1 - d0)) * 100;

    const DAY = 86400000;
    const fmtDate = (ms) => new Date(ms).toLocaleDateString(undefined, { day: "numeric", month: "short" });
    const fmtVal = (v) => (dated ? fmtDate(toNum(v)) : (opts.unit || "Unit") + " " + v);

    /* ---- ticks ---- */
    const ticks = [];
    if (dated) {
      const span = (d1 - d0) / DAY;
      if (span > 200) {                              /* quarter starts */
        const c = new Date(d0); c.setDate(1); c.setMonth(Math.floor(c.getMonth() / 3) * 3);
        while (c.getTime() <= d1) {
          if (c.getTime() >= d0) ticks.push({ at: c.getTime(),
            t: c.toLocaleDateString(undefined, { month: "short" }), d: c.getFullYear() });
          c.setMonth(c.getMonth() + 3);
        }
      } else if (span > 60) {                        /* month starts */
        const c = new Date(d0); c.setDate(1);
        while (c.getTime() <= d1) {
          if (c.getTime() >= d0) ticks.push({ at: c.getTime(),
            t: c.toLocaleDateString(undefined, { month: "short" }), d: c.getFullYear() });
          c.setMonth(c.getMonth() + 1);
        }
      } else {                                       /* weekly */
        const c = new Date(d0); c.setHours(0, 0, 0, 0);
        c.setDate(c.getDate() + ((8 - c.getDay()) % 7));
        while (c.getTime() <= d1) {
          ticks.push({ at: c.getTime(), t: fmtDate(c.getTime()), d: "" });
          c.setDate(c.getDate() + 7);
        }
      }
    } else {
      const n = Math.min(12, Math.max(4, Math.round((d1 - d0))));
      const raw = (d1 - d0) / n;
      const pw = Math.pow(10, Math.floor(Math.log10(raw) || 0));
      const m = raw / pw;
      const stp = (m <= 1 ? 1 : m <= 2 ? 2 : m <= 5 ? 5 : 10) * pw;
      for (let v = Math.ceil(d0 / stp) * stp; v <= d1 + 1e-9; v += stp) {
        ticks.push({ at: v, t: String(Math.round(v * 100) / 100), d: "" });
      }
    }

    const p = [];
    p.push('<div class="scroller"><div class="gt-chart">');

    /* ---- axis ---- */
    p.push('<div class="gt-axis"><div class="gt-caption">' + esc(opts.caption || "") + "</div><div class=\"gt-ticks\">");
    ticks.forEach((t, i) => {
      const cls = i === 0 ? " first" : i === ticks.length - 1 ? " last" : "";
      p.push('<span class="gt-tick' + cls + '" style="left:' + pct(t.at).toFixed(2) + '%">' +
        esc(t.t) + (t.d ? '<span class="d">' + esc(String(t.d)) + "</span>" : "") + "</span>");
    });
    p.push("</div></div>");

    /* ---- flag lane ---- */
    if ((opts.marks || []).length) {
      p.push('<div class="gt-flags"><div></div><div class="gt-lane">');
      opts.marks.forEach((m) => {
        const x = pct(m.at);
        const pin = x < 8 ? " pin-left" : x > 92 ? " pin-right" : "";
        p.push('<span class="gt-flag' + pin + '" style="left:' + x.toFixed(2) + '%">' + esc(m.label) + "</span>");
      });
      p.push("</div></div>");
    }

    /* ---- background layer ---- */
    p.push('<div class="gt-body"><div class="gt-bg"><div></div><div>');
    (opts.bands || []).forEach((b) => {
      const a = pct(b.from), z = pct(b.to);
      p.push('<span class="gt-band" style="left:' + a.toFixed(2) + '%;width:' + (z - a).toFixed(2) + '%"></span>');
    });
    (opts.lines || []).forEach((l) => {
      p.push('<span class="gt-vline" style="left:' + pct(l.at).toFixed(2) + '%"></span>');
    });
    if (opts.today != null) {
      p.push('<span class="gt-today" style="left:' + pct(opts.today).toFixed(2) + '%"></span>');
    }
    p.push('</div></div><div class="gt-rows">');

    /* ---- rows ---- */
    rows.forEach((r, i) => {
      if (r.group) {
        p.push('<div class="gt-row is-head"><div class="gt-group">' + esc(r.group) + "</div></div>");
        return;
      }
      const cat = cats[r.cat] || {};
      const ci = cat.c != null ? cat.c : 0;

      p.push('<div class="gt-row"><div class="gt-label">' +
        '<span class="nm">' + esc(r.label) + "</span>" +
        (r.ids ? '<span class="ids">' + esc(r.ids) + "</span>" : "") +
        (r.owner ? '<span class="gt-owner">' + esc(r.owner) + "</span>" : "") +
        '</div><div class="gt-track">');

      const tipHtml =
        '<span class="t">' + esc(r.label) + "</span>" +
        (cat.label ? '<span class="r">' + esc(cat.label) + "</span>" : "") +
        '<span class="r">' + (r.milestone || r.at != null
          ? esc(fmtVal(r.at))
          : "<b>" + esc(fmtVal(r.from)) + "</b> to <b>" + esc(fmtVal(r.to)) + "</b>") + "</span>" +
        (r.owner ? '<span class="r">Owner <b>' + esc(r.owner) + "</b></span>" : "") +
        (r.tip ? '<span class="r">' + r.tip + "</span>" : "");

      if (r.milestone || (r.at != null && r.from == null)) {
        p.push('<span class="gt-diamond gt-c' + ci + '" style="left:' + pct(r.at).toFixed(2) +
          '%" data-tip="' + esc(tipHtml) + '" title="' + esc(r.label) + '"></span>');
      } else {
        const a = pct(r.from), w = pct(r.to) - a;
        const px = (v) => (v / 100) * trackPx;
        const need = r.label.length * 6.6 + 18;
        const inside = px(w) > need;
        const goRight = !inside && px(100 - (a + w)) > need;
        const goLeft  = !inside && !goRight && px(a) > need;

        p.push('<span class="gt-bar gt-c' + ci +
          '" style="left:' + a.toFixed(2) + '%;width:' + Math.max(w, 0.6).toFixed(2) + '%"' +
          ' data-tip="' + esc(tipHtml) + '">' + (inside || (!goRight && !goLeft) ? esc(r.label) : "") + "</span>");

        if (goRight || goLeft) {
          p.push('<span class="gt-outlabel" style="' +
            (goRight ? "left:" + (a + w).toFixed(2) + "%;padding-left:8px"
                     : "right:" + (100 - a).toFixed(2) + "%;padding-right:8px") +
            '">' + esc(r.label) + "</span>");
        }
      }
      p.push("</div></div>");
    });

    p.push("</div></div></div></div>");

    /* ---- legend ---- */
    if (opts.legend !== false && Object.keys(cats).length) {
      p.push('<div class="legend">');
      Object.keys(cats).forEach((k) => {
        const c = cats[k];
        p.push("<span><i class=\"swatch c" + (c.c != null ? c.c : 0) + "\"></i>" +
          esc(k + (c.label ? " · " + c.label : "")) + "</span>");
      });
      p.push("</div>");
    }

    el.classList.add("gt");
    el.innerHTML = p.join("");
    $$("[data-tip]", el).forEach((n) => bindTip(n, n.getAttribute("data-tip")));
  }

  /* ---------- 8. GRAPH DIAGRAM --------------------------------------------
     Report.graph(hostSelectorOrNode, nodes, edges, opts)

     Structure and flow: schemas, pipelines, service maps, state machines.
     You give it what connects to what; it derives every coordinate, every
     arrowhead and every self-loop. Nothing has to be kept in sync by hand.

     nodes: [{ id, label, sub, cat, col, row, tip }]
       id     required, referenced by edges
       label  the box text; wraps to at most three lines
       sub    a second, smaller line — a type, a count, a store
       cat    0-5, tints the border from --c0…--c5
       col    0-based column. Omit and it is derived from the edges.
       row    0-based row within the column. Omit and declaration order wins.
       tip    tooltip HTML, shown when #tip exists

     edges: [{ from, to, label, kind, dir }]
       from,to  node ids. from === to draws a self-loop.
       label    placed on the line, with a halo so it stays readable
       kind     "alt" for the dashed accent style
       dir      "both" for a head at each end, "none" for a plain line

     opts:
       nodeW,nodeH  box size in user units (default 168 x 56)
       colGap,rowGap  space between boxes (default 78 / 34)
       groups   [{ label, nodes: [id, …] }] dashed enclosure with a caption
       label    aria-label for the figure
       tidy     false to keep back-edges straight instead of curving them

     Renders an <svg> into the host. Twelve nodes is a comfortable ceiling;
     past that, split the diagram or move the detail into a table.
  ------------------------------------------------------------------------ */
  let graphSeq = 0;

  function graph(host, nodes, edges, opts) {
    const el = typeof host === "string" ? $(host) : host;
    if (!el || !Array.isArray(nodes) || !nodes.length) return;
    edges = Array.isArray(edges) ? edges : [];
    opts = opts || {};

    const NW = opts.nodeW || 168, NH = opts.nodeH || 56;
    const GY = opts.rowGap || 34;
    const uid = "g" + (++graphSeq);

    const byId = {};
    nodes.forEach((n) => { if (n && n.id != null) byId[n.id] = n; });
    const links = edges.filter((e) => e && byId[e.from] && byId[e.to]);
    const flows = links.filter((e) => e.from !== e.to);
    const loops = links.filter((e) => e.from === e.to);

    /* ---- columns: honour what was given, derive the rest from the edges.
           Edges that close a cycle are held out of the layering, so a feedback
           path bows backwards instead of dragging the chain to the right. ---- */
    const fanout = {};
    flows.forEach((e, i) => { (fanout[e.from] = fanout[e.from] || []).push(i); });
    const mark = {}, closes = {};
    (function () {
      const walk = (id) => {
        mark[id] = 1;
        (fanout[id] || []).forEach((i) => {
          const to = flows[i].to;
          if (mark[to] === 1) closes[i] = true;             /* target is an ancestor */
          else if (!mark[to]) walk(to);
        });
        mark[id] = 2;
      };
      nodes.forEach((n) => { if (!mark[n.id]) walk(n.id); });
    })();
    const forward = flows.filter((e, i) => !closes[i]);

    const col = {};
    nodes.forEach((n) => { col[n.id] = n.col != null ? n.col : 0; });
    const fixed = (id) => byId[id].col != null;
    for (let pass = 0; pass <= nodes.length; pass++) {
      let moved = false;
      forward.forEach((e) => {
        if (fixed(e.to)) return;
        const want = col[e.from] + 1;
        if (want > col[e.to]) { col[e.to] = want; moved = true; }
      });
      if (!moved) break;
    }
    const c0 = Math.min.apply(null, nodes.map((n) => col[n.id]));
    if (c0) nodes.forEach((n) => { col[n.id] -= c0; });      /* no empty leading column */

    /* ---- rows: given, else declaration order within the column ---- */
    const used = {}, row = {};
    nodes.forEach((n) => {
      const c = col[n.id];
      used[c] = used[c] || [];
      if (n.row != null) { row[n.id] = n.row; used[c].push(n.row); }
    });
    nodes.forEach((n) => {
      if (row[n.id] != null) return;
      const c = col[n.id];
      let r = 0;
      while (used[c].indexOf(r) !== -1) r++;
      row[n.id] = r;
      used[c].push(r);
    });

    /* Column gaps widen for the longest edge label, so a label never has to sit
       on top of the boxes it runs between. */
    let need = 0;
    flows.forEach((e) => {
      if (e.label && col[e.to] !== col[e.from]) need = Math.max(need, String(e.label).length * 5.8 + 16);
    });
    const GX = Math.max(opts.colGap || 78, Math.min(need, 190));

    const r0 = Math.min.apply(null, nodes.map((n) => row[n.id]));
    if (r0) nodes.forEach((n) => { row[n.id] -= r0; });

    const maxCol = Math.max.apply(null, nodes.map((n) => col[n.id]));
    const maxRow = Math.max.apply(null, nodes.map((n) => row[n.id]));
    const M = { t: loops.length ? 58 : 22, r: 22, b: 22, l: 22 };
    const W = M.l + (maxCol + 1) * NW + maxCol * GX + M.r;
    const H = M.t + (maxRow + 1) * NH + maxRow * GY + M.b;

    const at = (n) => ({
      x: M.l + col[n.id] * (NW + GX) + NW / 2,
      y: M.t + row[n.id] * (NH + GY) + NH / 2
    });
    const pos = {};
    nodes.forEach((n) => { pos[n.id] = at(n); });

    /* ---- geometry: where a straight line leaves a box, with a 5-unit gap ---- */
    const border = (p, q) => {
      const dx = q.x - p.x, dy = q.y - p.y;
      if (!dx && !dy) return { x: p.x, y: p.y };
      const s = Math.min(
        dx ? (NW / 2) / Math.abs(dx) : Infinity,
        dy ? (NH / 2) / Math.abs(dy) : Infinity
      );
      const len = Math.sqrt(dx * dx + dy * dy);
      const gap = 5 / len;
      return { x: p.x + dx * (s + gap), y: p.y + dy * (s + gap) };
    };

    const wrap = (text, max, lines) => {
      const out = [];
      let line = "";
      String(text == null ? "" : text).split(/\s+/).forEach((w) => {
        if (!line.length) line = w;
        else if ((line + " " + w).length <= max) line += " " + w;
        else { out.push(line); line = w; }
      });
      if (line) out.push(line);
      if (out.length > lines) { out.length = lines; out[lines - 1] = out[lines - 1].slice(0, max - 1) + "…"; }
      return out;
    };

    const p = [];
    /* max-width stops a small diagram from being blown up to the container
       width, so type stays the same size in every figure in the report */
    p.push('<svg viewBox="0 0 ' + W + " " + H + '" style="max-width:' + W + "px;min-width:" +
      Math.min(W, 640) + 'px" xmlns="http://www.w3.org/2000/svg" role="img" aria-label="' +
      esc(opts.label || "Structure diagram") + '">');

    /* ---- arrowheads live in <marker>, so a head can never drift from its line ---- */
    p.push('<defs>');
    [["", "s-arrow"], ["-alt", "s-arrow-alt"]].forEach((m) => {
      p.push('<marker id="' + uid + '-arrow' + m[0] + '" viewBox="0 0 10 10" refX="9" refY="5" ' +
        'markerWidth="7" markerHeight="7" orient="auto-start-reverse">' +
        '<path class="' + m[1] + '" d="M0,0 L10,5 L0,10 Z"/></marker>');
    });
    p.push('</defs>');

    /* ---- group enclosures, behind everything ---- */
    (opts.groups || []).forEach((g) => {
      const pts = (g.nodes || []).filter((id) => pos[id]).map((id) => pos[id]);
      if (!pts.length) return;
      const pad = 16;
      const x0 = Math.min.apply(null, pts.map((q) => q.x)) - NW / 2 - pad;
      const x1 = Math.max.apply(null, pts.map((q) => q.x)) + NW / 2 + pad;
      const y0 = Math.min.apply(null, pts.map((q) => q.y)) - NH / 2 - pad;
      const y1 = Math.max.apply(null, pts.map((q) => q.y)) + NH / 2 + pad;
      p.push('<rect class="s-grp" x="' + x0.toFixed(1) + '" y="' + y0.toFixed(1) +
        '" width="' + (x1 - x0).toFixed(1) + '" height="' + (y1 - y0).toFixed(1) + '" rx="10"/>');
      if (g.label) {
        p.push('<text class="s-grplab" x="' + (x0 + 4).toFixed(1) + '" y="' + (y0 - 6).toFixed(1) +
          '">' + esc(String(g.label).toUpperCase()) + "</text>");
      }
    });

    /* ---- edges ---- */
    const edge = (d, e, lab) => {
      const cls = e.kind === "alt" ? "s-edge-alt" : "s-edge";
      const head = e.kind === "alt" ? uid + "-arrow-alt" : uid + "-arrow";
      let a = '<path class="' + cls + '" d="' + d + '"';
      if (e.dir !== "none") a += ' marker-end="url(#' + head + ')"';
      if (e.dir === "both") a += ' marker-start="url(#' + head + ')"';
      if (e.tip) a += ' data-tip="' + esc(e.tip) + '"';
      a += "><title>" + esc((e.from) + " → " + (e.to) + (e.label ? " — " + e.label : "")) + "</title></path>";
      p.push(a);
      if (e.label) {
        p.push('<text class="s-edgelab" x="' + lab.x.toFixed(1) + '" y="' + lab.y.toFixed(1) +
          '" text-anchor="middle">' + esc(e.label) + "</text>");
      }
    };

    flows.forEach((e) => {
      const A = pos[e.from], B = pos[e.to];
      const back = col[e.to] < col[e.from] || (col[e.to] === col[e.from] && row[e.to] < row[e.from]);
      const a = border(A, B), b = border(B, A);
      if (back && opts.tidy !== false) {
        /* a returning edge bows clear of the forward ones it runs beside */
        const mx = (a.x + b.x) / 2, my = (a.y + b.y) / 2;
        const dx = b.x - a.x, dy = b.y - a.y;
        const len = Math.max(1, Math.sqrt(dx * dx + dy * dy));
        const bow = Math.min(52, len * 0.22);
        const cxp = mx + (dy / len) * bow, cyp = my - (dx / len) * bow;
        edge("M" + a.x.toFixed(1) + "," + a.y.toFixed(1) + " Q" + cxp.toFixed(1) + "," + cyp.toFixed(1) +
          " " + b.x.toFixed(1) + "," + b.y.toFixed(1), e,
          { x: (mx + cxp) / 2, y: (my + cyp) / 2 - 4 });
      } else {
        /* a mostly-horizontal label wider than its run is lifted clear of the boxes.
           A vertical edge keeps its label on the line: the lane above the box
           belongs to the self-loop. */
        const run = Math.abs(b.x - a.x);
        const wide = e.label && run >= Math.abs(b.y - a.y) && String(e.label).length * 5.8 > run;
        edge("M" + a.x.toFixed(1) + "," + a.y.toFixed(1) + " L" + b.x.toFixed(1) + "," + b.y.toFixed(1), e,
          wide ? { x: (a.x + b.x) / 2, y: Math.min(A.y, B.y) - NH / 2 - 7 }
               : { x: (a.x + b.x) / 2, y: (a.y + b.y) / 2 - 5 });
      }
    });

    /* ---- self-loops: one convention, drawn the same way every time.
           An arc over the top of the box, left shoulder to right shoulder,
           head landing back on the box. Its label sits above the arc. ---- */
    loops.forEach((e) => {
      const n = pos[e.from];
      const x1 = n.x - NW * 0.20, x2 = n.x + NW * 0.20, top = n.y - NH / 2;
      const rise = 30;
      edge("M" + x1.toFixed(1) + "," + (top - 3).toFixed(1) +
        " C" + x1.toFixed(1) + "," + (top - rise - 12).toFixed(1) +
        " " + x2.toFixed(1) + "," + (top - rise - 12).toFixed(1) +
        " " + x2.toFixed(1) + "," + (top - 3).toFixed(1), e,
        { x: n.x, y: top - rise - 16 });
    });

    /* ---- nodes, on top of the edges ---- */
    nodes.forEach((n) => {
      const q = pos[n.id];
      const x = q.x - NW / 2, y = q.y - NH / 2;
      const accent = n.cat != null ? ' class="s-node is-accent" style="--hue: var(--c' + n.cat + ')"'
                                   : ' class="s-node"';
      p.push("<rect" + accent + ' x="' + x.toFixed(1) + '" y="' + y.toFixed(1) + '" width="' + NW +
        '" height="' + NH + '" rx="8"' + (n.tip ? ' data-tip="' + esc(n.tip) + '"' : "") + "/>");

      const lines = wrap(n.label != null ? n.label : n.id, Math.floor(NW / 7.4), n.sub ? 2 : 3);
      const block = lines.length * 15 + (n.sub ? 13 : 0);
      let ty = q.y - block / 2 + 12;
      lines.forEach((line) => {
        p.push('<text class="s-lab" x="' + q.x + '" y="' + ty.toFixed(1) +
          '" text-anchor="middle">' + esc(line) + "</text>");
        ty += 15;
      });
      if (n.sub) {
        p.push('<text class="s-sub" x="' + q.x + '" y="' + (ty + 1).toFixed(1) +
          '" text-anchor="middle">' + esc(n.sub) + "</text>");
      }
    });

    p.push("</svg>");
    el.classList.add("gph");
    el.innerHTML = p.join("");
    $$("[data-tip]", el).forEach((n) => bindTip(n, n.getAttribute("data-tip")));
  }

  /* ---------- boot ---------- */
  function boot() { initTheme(); initTips(); initSort(); initScrollspy(); initStamp(); }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot);
  else boot();

  /* Exposed for report-specific code appended below this block. */
  window.Report = { $, $$, esc, bindTip, waterfall, gantt, graph };
})();
