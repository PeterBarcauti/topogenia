// aide.js
// The help of Topogenia: the summary follows the reading, a search through every section, the captures enlarged, the progress of the reading; no library, nothing sent anywhere.

(() => {
  'use strict';

  const root = document.documentElement;
  root.classList.add('js');
  const one = (selector, scope = document) => scope.querySelector(selector);
  const all = (selector, scope = document) => Array.from(scope.querySelectorAll(selector));
  /** A text compared without its case nor its accents: « Étape » finds « etape ». */
  const fold = (text) => text.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLowerCase();

  const sidebar = one('#sidebar');
  const toggle = one('#nav-toggle');
  const bar = one('#progress');
  const upward = one('#to-top');

  // The summary of a small screen: opened by its button, closed by a link chosen in it or by Escape.
  const showNav = (open) => {
    root.classList.toggle('nav-open', open);
    toggle.setAttribute('aria-expanded', String(open));
  };
  toggle.addEventListener('click', () => { showNav(!root.classList.contains('nav-open')); });
  sidebar.addEventListener('click', (event) => { if (event.target.closest('a')) showNav(false); });

  // The section read: the last heading above the top of the window, its link marked and its chapter opened.
  const links = new Map(all('#sidebar a[href^="#"]').map((link) => [link.getAttribute('href').slice(1), link]));
  const headings = all('main h2[id], main h3[id]').filter((heading) => links.has(heading.id));
  let current = null;
  const mark = (id) => {
    if (id === current) return;
    current = id;
    all('#sidebar [aria-current]').forEach((link) => { link.removeAttribute('aria-current'); });
    all('.toc-chapter').forEach((chapter) => { chapter.classList.remove('is-current', 'is-open'); });
    const link = id === null ? undefined : links.get(id);
    if (link === undefined) return;
    link.setAttribute('aria-current', 'location');
    link.closest('.toc-chapter').classList.add('is-current', 'is-open');
    const box = link.getBoundingClientRect();
    const frame = sidebar.getBoundingClientRect();
    // The summary scrolls alone: scrollIntoView would move the page too, and stop the scroll to an anchor on its way.
    if (box.top < frame.top + 48) sidebar.scrollTop -= frame.top + 48 - box.top;
    else if (box.bottom > frame.bottom - 48) sidebar.scrollTop += box.bottom - (frame.bottom - 48);
  };

  let pending = false;
  const follow = () => {
    pending = false;
    const height = root.scrollHeight - root.clientHeight;
    bar.style.transform = `scaleX(${String(height > 0 ? Math.min(1, root.scrollTop / height) : 0)})`;
    upward.hidden = root.scrollTop < 900;
    let read = null;
    for (const heading of headings) {
      if (heading.getBoundingClientRect().top > 140) break;
      read = heading.id;
    }
    mark(read);
  };
  const later = () => {
    if (!pending) {
      pending = true;
      requestAnimationFrame(follow);
    }
  };
  addEventListener('scroll', later, { passive: true });
  addEventListener('resize', later);
  follow();
  upward.addEventListener('click', () => { scrollTo({ top: 0 }); });

  // What comes into view rises gently once.
  if ('IntersectionObserver' in window) {
    const watcher = new IntersectionObserver((entries) => {
      for (const entry of entries) {
        if (entry.isIntersecting) {
          entry.target.classList.add('is-visible');
          watcher.unobserve(entry.target);
        }
      }
    }, { rootMargin: '0px 0px -8% 0px' });
    all('.reveal').forEach((element) => { watcher.observe(element); });
  } else {
    all('.reveal').forEach((element) => { element.classList.add('is-visible'); });
  }

  // The captures enlarged in a dialog: a click, Escape or a click outside to close.
  const viewer = one('#lightbox');
  const picture = one('#lightbox-img');
  const legend = one('#lightbox-caption');
  all('.zoom').forEach((button) => {
    button.addEventListener('click', () => {
      const image = one('img', button);
      picture.src = image.currentSrc || image.src;
      picture.alt = image.alt;
      legend.textContent = image.alt;
      viewer.showModal();
    });
  });
  viewer.addEventListener('click', (event) => {
    if (event.target === viewer || event.target.closest('.lightbox-close')) viewer.close();
  });

  // The search: every section of the help, its number, its title and its text; all the words asked must be in it.
  const input = one('#search');
  const list = one('#results');
  const sections = all('main h3[id]').map((heading) => {
    const parts = [];
    for (let node = heading.nextElementSibling; node !== null && !/^H[23]$/.test(node.tagName); node = node.nextElementSibling) parts.push(node.textContent);
    const chapter = heading.closest('.chapter');
    const title = one('.text', heading)?.textContent ?? heading.textContent;
    const text = parts.join(' ').replace(/\s+/g, ' ').trim();
    return {
      id: heading.id,
      number: one('.num', heading)?.textContent ?? '',
      title,
      chapter: one('.chapter-head h2 .text', chapter)?.textContent ?? '',
      text,
      foldedTitle: fold(title),
      foldedText: fold(text),
    };
  });

  /** A part of a text with the words asked marked, built as nodes: the search never writes HTML. */
  const marked = (text, words) => {
    const span = document.createElement('span');
    const folded = fold(text);
    const ranges = [];
    for (const word of words) {
      for (let at = folded.indexOf(word); at !== -1; at = folded.indexOf(word, at + word.length)) ranges.push([at, at + word.length]);
    }
    ranges.sort((a, b) => a[0] - b[0]);
    let done = 0;
    for (const [start, end] of ranges) {
      if (start < done) continue;
      span.append(text.slice(done, start));
      const hit = document.createElement('mark');
      hit.textContent = text.slice(start, end);
      span.append(hit);
      done = end;
    }
    span.append(text.slice(done));
    return span;
  };

  /** Around the first word found, a passage of the text; its start and end cut at a space. */
  const passage = (section, words) => {
    const at = Math.max(0, section.foldedText.indexOf(words[0]));
    let start = Math.max(0, at - 60);
    let end = Math.min(section.text.length, at + 120);
    if (start > 0) start = section.text.indexOf(' ', start) + 1;
    if (end < section.text.length) end = Math.max(at + words[0].length, section.text.lastIndexOf(' ', end));
    return `${start > 0 ? '… ' : ''}${section.text.slice(start, end)}${end < section.text.length ? ' …' : ''}`;
  };

  let found = [];
  let chosen = -1;
  const choose = (index) => {
    chosen = index;
    all('.result', list).forEach((item, at) => { item.setAttribute('aria-selected', String(at === index)); });
    const item = all('.result', list)[index];
    if (item !== undefined) {
      const [top, bottom] = [item.offsetTop, item.offsetTop + item.offsetHeight];
      if (top < list.scrollTop) list.scrollTop = top;
      else if (bottom > list.scrollTop + list.clientHeight) list.scrollTop = bottom - list.clientHeight;
      input.setAttribute('aria-activedescendant', item.id);
    } else input.removeAttribute('aria-activedescendant');
  };
  const close = () => {
    list.hidden = true;
    input.setAttribute('aria-expanded', 'false');
    input.removeAttribute('aria-activedescendant');
    chosen = -1;
  };
  const search = () => {
    const words = fold(input.value).split(/\s+/).filter((word) => word.length > 1);
    list.replaceChildren();
    if (words.length === 0) {
      close();
      return;
    }
    found = sections
      .filter((section) => words.every((word) => section.foldedTitle.includes(word) || section.foldedText.includes(word)))
      .map((section) => ({
        section,
        score: words.reduce((sum, word) => sum + (section.foldedTitle.includes(word) ? 10 : 0) + section.foldedText.split(word).length - 1, 0),
      }))
      .sort((a, b) => b.score - a.score)
      .slice(0, 8)
      .map((item) => item.section);
    if (found.length === 0) {
      const empty = document.createElement('div');
      empty.className = 'results-empty';
      // The sentence is the one of the language of the page, written in its markup: « {q} » is the text searched.
      empty.textContent = (list.dataset.empty ?? '').replace('{q}', input.value.trim());
      list.append(empty);
    }
    found.forEach((section, index) => {
      const item = document.createElement('a');
      item.className = 'result';
      item.id = `result-${String(index)}`;
      item.href = `#${section.id}`;
      item.setAttribute('role', 'option');
      item.setAttribute('aria-selected', 'false');
      const path = document.createElement('span');
      path.className = 'result-path';
      path.textContent = `${section.number} · ${section.chapter}`;
      const title = marked(section.title, words);
      title.className = 'result-title';
      item.append(path, title);
      if (section.text !== '') {
        const text = marked(passage(section, words), words);
        text.className = 'result-text';
        item.append(text);
      }
      item.addEventListener('click', () => {
        close();
        input.blur();
      });
      list.append(item);
    });
    list.hidden = false;
    input.setAttribute('aria-expanded', 'true');
    choose(found.length > 0 ? 0 : -1);
  };
  input.addEventListener('input', search);
  input.addEventListener('focus', () => { if (input.value.trim() !== '') search(); });
  input.addEventListener('keydown', (event) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (found.length > 0) choose((chosen + (event.key === 'ArrowDown' ? 1 : found.length - 1)) % found.length);
    } else if (event.key === 'Enter') {
      const item = all('.result', list)[Math.max(0, chosen)];
      if (item !== undefined) {
        event.preventDefault();
        location.hash = item.getAttribute('href');
        close();
        input.blur();
      }
    } else if (event.key === 'Escape') {
      if (input.value !== '') input.value = '';
      close();
      input.blur();
    }
  });
  document.addEventListener('click', (event) => { if (!event.target.closest('.search')) close(); });

  // The section aimed by a link, the search or the address, while the page still scrolls toward it (the scroll is smooth):
  // the section read on the way is not the one asked (F-124). Kept until the page reaches it, or the person scrolls herself.
  let aimed = location.hash.slice(1) || null;
  addEventListener('hashchange', () => { aimed = location.hash.slice(1) || null; });
  const forget = () => { aimed = null; };
  addEventListener('wheel', forget, { passive: true });
  addEventListener('touchmove', forget, { passive: true });
  addEventListener('keydown', (event) => { if (['PageDown', 'PageUp', 'ArrowDown', 'ArrowUp', 'Home', 'End', ' '].includes(event.key)) forget(); });
  addEventListener('scroll', () => { requestAnimationFrame(() => { if (aimed !== null && aimed === current) forget(); }); }, { passive: true });

  // The other language of the help opens at the same place: both are made from one manual, their sections bear the same names.
  const other = one('.lang-switch');
  if (other !== null) {
    other.addEventListener('click', () => {
      const target = aimed ?? current ?? location.hash.slice(1);
      other.href = `${other.getAttribute('href').split('#')[0]}${target === '' ? '' : `#${target}`}`;
    });
  }

  // « / » or Ctrl+K: to the search, from anywhere but a field.
  document.addEventListener('keydown', (event) => {
    const typing = event.target instanceof HTMLElement && event.target.closest('input, textarea, select') !== null;
    if (((event.key === '/' && !typing) || ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k')) && !viewer.open) {
      event.preventDefault();
      input.focus();
      input.select();
    } else if (event.key === 'Escape' && root.classList.contains('nav-open')) showNav(false);
  });
})();
