// Live SEO/GEO readiness score for the Create/Edit Resource form.
//
// Mirrors Helpers/ResourceSeoScorer.cs exactly (same checklist, same weighting: score = passed / total
// checklist items, rounded). This duplication is intentional and unavoidable — the C# version is the
// server-side source of truth (used on save and on the Manage Resources list score chip), this JS
// version drives the live gauge/checklist while the admin is typing, with no round trip. If the
// checklist in ResourceSeoScorer.cs changes, update this function to match.
(function () {
  'use strict';

  function computeSeoScore(fields) {
    var checklist = [
      { label: 'Slug set', passed: !!(fields.slug && fields.slug.trim()) },
      { label: 'Meta title ≤ 60 chars', passed: !!(fields.metaTitle && fields.metaTitle.trim() && fields.metaTitle.length <= 60) },
      { label: 'Meta description ≤ 160 chars', passed: !!(fields.metaDescription && fields.metaDescription.trim() && fields.metaDescription.length <= 160) },
      { label: 'Focus keyword set', passed: !!(fields.focusKeyword && fields.focusKeyword.trim()) },
      { label: 'Canonical URL set', passed: !!(fields.canonicalUrl && fields.canonicalUrl.trim()) },
      { label: 'Image alt text set', passed: !!(fields.imageAlt && fields.imageAlt.trim()) },
      { label: 'Author set', passed: !!(fields.author && fields.author.trim()) },
      { label: 'AI summary (TL;DR) set', passed: !!(fields.tldr && fields.tldr.trim()) },
      { label: 'Schema type set', passed: true }
    ];

    if (fields.schemaType === 'FAQ') {
      var validFaqCount = (fields.faqItems || []).filter(function (q) {
        return q.question && q.question.trim() && q.answer && q.answer.trim();
      }).length;
      checklist.push({ label: 'FAQ has 2+ questions', passed: validFaqCount >= 2 });
    }

    var passed = checklist.filter(function (c) { return c.passed; }).length;
    var score = checklist.length === 0 ? 0 : Math.round((passed * 100) / checklist.length);

    return { score: score, checklist: checklist };
  }

  function tierClass(score) {
    if (score >= 80) return 'success';
    if (score >= 50) return 'warning';
    return 'danger';
  }

  // ── Form wiring (Create.cshtml / Edit.cshtml share this exact markup/ID structure) ─────────────
  function initResourceSeoForm() {
    var titleInput = document.getElementById('Resource_Title');
    var slugInput = document.getElementById('Resource_Slug');
    var slugResetLink = document.getElementById('slugResetLink');
    var slugManuallyEdited = !!(slugInput && slugInput.value && slugInput.value.trim());

    function slugify(text) {
      return (text || '')
        .toLowerCase()
        .normalize('NFD').replace(/[̀-ͯ]/g, '')
        .replace(/[^a-z0-9\s-]/g, '')
        .replace(/[\s-]+/g, '-')
        .replace(/^-+|-+$/g, '');
    }

    if (titleInput && slugInput) {
      titleInput.addEventListener('input', function () {
        if (!slugManuallyEdited) {
          slugInput.value = slugify(titleInput.value);
          recompute();
        }
      });
      slugInput.addEventListener('input', function () {
        slugManuallyEdited = true;
        if (slugResetLink) slugResetLink.classList.remove('d-none');
        recompute();
      });
      if (slugResetLink) {
        slugResetLink.addEventListener('click', function (e) {
          e.preventDefault();
          slugManuallyEdited = false;
          slugInput.value = slugify(titleInput.value);
          slugResetLink.classList.add('d-none');
          recompute();
        });
        if (slugManuallyEdited) slugResetLink.classList.remove('d-none');
      }
    }

    // Character counters
    document.querySelectorAll('[data-char-counter-for]').forEach(function (counterEl) {
      var targetId = counterEl.getAttribute('data-char-counter-for');
      var max = parseInt(counterEl.getAttribute('data-char-max'), 10) || 0;
      var targetEl = document.getElementById(targetId);
      if (!targetEl) return;
      function update() {
        var len = (targetEl.value || '').length;
        counterEl.textContent = len + ' / ' + max;
        counterEl.classList.toggle('text-danger', len > max);
      }
      targetEl.addEventListener('input', update);
      update();
    });

    // Schema type -> FAQ builder visibility.
    // Html.GetEnumSelectList<T>() renders <option value="0|1|2"> (the numeric enum ordinal), with the
    // enum member name only as the visible <option> text — so compare against the selected option's
    // TEXT, not its value, or this never matches regardless of which member is "FAQ".
    var schemaTypeSelect = document.getElementById('Resource_SchemaType');
    var faqSection = document.getElementById('faqBuilderSection');
    function selectedSchemaTypeName() {
      if (!schemaTypeSelect || schemaTypeSelect.selectedIndex < 0) return '';
      return schemaTypeSelect.options[schemaTypeSelect.selectedIndex].text;
    }
    function syncFaqVisibility() {
      if (!schemaTypeSelect || !faqSection) return;
      faqSection.classList.toggle('d-none', selectedSchemaTypeName() !== 'FAQ');
    }
    if (schemaTypeSelect) {
      schemaTypeSelect.addEventListener('change', function () {
        syncFaqVisibility();
        recompute();
      });
      syncFaqVisibility();
    }

    // FAQ builder: add/remove rows
    var faqList = document.getElementById('faqItemsList');
    var addFaqBtn = document.getElementById('addFaqItemBtn');
    function faqRowTemplate(index, question, answer) {
      var row = document.createElement('div');
      row.className = 'border rounded p-2 mb-2 faq-item-row';
      row.innerHTML =
        '<div class="mb-2">' +
        '  <label class="form-label small mb-1">Question</label>' +
        '  <input type="text" class="form-control form-control-sm" name="Resource.FaqItems[' + index + '].Question" value="' + escapeHtml(question) + '" />' +
        '</div>' +
        '<div class="mb-2">' +
        '  <label class="form-label small mb-1">Answer</label>' +
        '  <textarea class="form-control form-control-sm" rows="2" name="Resource.FaqItems[' + index + '].Answer">' + escapeHtml(answer) + '</textarea>' +
        '</div>' +
        '<button type="button" class="btn btn-sm btn-outline-danger faq-remove-btn">Remove</button>';
      return row;
    }
    function escapeHtml(s) {
      var div = document.createElement('div');
      div.textContent = s || '';
      return div.innerHTML;
    }
    function reindexFaqRows() {
      if (!faqList) return;
      var rows = faqList.querySelectorAll('.faq-item-row');
      rows.forEach(function (row, index) {
        row.querySelectorAll('input,textarea').forEach(function (el) {
          el.name = el.name.replace(/FaqItems\[\d+\]/, 'FaqItems[' + index + ']');
        });
      });
    }
    if (addFaqBtn && faqList) {
      addFaqBtn.addEventListener('click', function () {
        var index = faqList.querySelectorAll('.faq-item-row').length;
        faqList.appendChild(faqRowTemplate(index, '', ''));
        recompute();
      });
      faqList.addEventListener('click', function (e) {
        if (e.target && e.target.classList.contains('faq-remove-btn')) {
          e.target.closest('.faq-item-row').remove();
          reindexFaqRows();
          recompute();
        }
      });
      faqList.addEventListener('input', recompute);
    }

    // Live gauge + checklist
    var gaugeValueEl = document.getElementById('seoGaugeValue');
    var gaugeCircleEl = document.getElementById('seoGaugeCircle');
    var checklistEl = document.getElementById('seoChecklist');

    function collectFields() {
      var faqItems = [];
      if (faqList) {
        faqList.querySelectorAll('.faq-item-row').forEach(function (row) {
          var q = row.querySelector('input[name*="Question"]');
          var a = row.querySelector('textarea[name*="Answer"]');
          faqItems.push({ question: q ? q.value : '', answer: a ? a.value : '' });
        });
      }
      return {
        slug: slugInput ? slugInput.value : '',
        metaTitle: valueOf('Resource_MetaTitle'),
        metaDescription: valueOf('Resource_MetaDescription'),
        focusKeyword: valueOf('Resource_FocusKeyword'),
        canonicalUrl: valueOf('Resource_CanonicalUrl'),
        imageAlt: valueOf('Resource_ImageAlt'),
        author: valueOf('Resource_Author'),
        tldr: valueOf('Resource_Tldr'),
        schemaType: schemaTypeSelect ? selectedSchemaTypeName() : 'Article',
        faqItems: faqItems
      };
    }
    function valueOf(id) {
      var el = document.getElementById(id);
      return el ? el.value : '';
    }

    function recompute() {
      var result = computeSeoScore(collectFields());
      var tier = tierClass(result.score);

      if (gaugeValueEl) gaugeValueEl.textContent = result.score;
      if (gaugeCircleEl) {
        gaugeCircleEl.setAttribute('stroke', tier === 'success' ? '#10b981' : tier === 'warning' ? '#f59e0b' : '#ef4444');
        var circumference = 2 * Math.PI * 40; // r=40
        var offset = circumference - (result.score / 100) * circumference;
        gaugeCircleEl.style.strokeDasharray = circumference;
        gaugeCircleEl.style.strokeDashoffset = offset;
      }
      if (checklistEl) {
        checklistEl.innerHTML = result.checklist.map(function (c) {
          var icon = c.passed ? 'ri-checkbox-circle-fill text-success' : 'ri-error-warning-line text-warning';
          return '<div class="d-flex align-items-center gap-2 small mb-1"><i class="' + icon + '"></i><span>' + escapeHtml(c.label) + '</span></div>';
        }).join('');
      }
    }

    // Recompute on any relevant input change (delegated on the form for simplicity/robustness).
    var form = document.querySelector('form.resource-form');
    if (form) {
      form.addEventListener('input', recompute);
      form.addEventListener('change', recompute);
    }

    recompute();
  }

  window.EinvResourceSeo = { computeSeoScore: computeSeoScore, tierClass: tierClass, initResourceSeoForm: initResourceSeoForm };
})();
