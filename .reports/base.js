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

  /* ---------- boot ---------- */
  function boot() { initTheme(); initTips(); initSort(); initScrollspy(); initStamp(); }
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", boot);
  else boot();

  /* Exposed for report-specific code appended below this block. */
  window.Report = { $, $$, esc, bindTip, waterfall, gantt };
})();
