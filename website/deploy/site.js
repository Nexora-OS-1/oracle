const navToggle = document.querySelector('.menu-toggle');
const nav = document.querySelector('#primary-nav');
navToggle?.addEventListener('click', () => {
  const open = navToggle.getAttribute('aria-expanded') !== 'true';
  navToggle.setAttribute('aria-expanded', String(open));
  navToggle.setAttribute('aria-label', open ? 'Close navigation' : 'Open navigation');
  nav?.classList.toggle('is-open', open);
});
nav?.querySelectorAll('a').forEach(link => link.addEventListener('click', () => {
  nav.classList.remove('is-open'); navToggle?.setAttribute('aria-expanded', 'false'); navToggle?.setAttribute('aria-label', 'Open navigation');
}));

async function loadRelease() {
  const links = [...document.querySelectorAll('[data-download]')];
  const status = document.querySelector('[data-download-status]');
  const ready = document.querySelector('[data-package-state]');
  try {
    const response = await fetch('release.json', { cache: 'no-store' });
    if (!response.ok) throw new Error('Release details are not available.');
    const release = await response.json();
    if (!release.package || !release.version || !release.sha256) throw new Error('Release details are incomplete.');
    const packageResponse = await fetch(release.package, { method: 'HEAD', cache: 'no-store' });
    if (!packageResponse.ok) throw new Error('The download package has not been published here yet.');
    for (const link of links) { link.href = release.package; link.setAttribute('download', ''); link.classList.remove('is-disabled'); link.removeAttribute('aria-disabled'); }
    document.querySelector('[data-version]').textContent = `ORACLE ${release.version}`;
    document.querySelector('[data-package-size]').textContent = release.size;
    document.querySelector('[data-package-sha]').textContent = release.sha256;
    document.querySelector('[data-package-type]').textContent = release.packageType;
    document.querySelector('[data-runtime]').textContent = release.selfContained ? 'Included in this self-contained package' : '.NET Desktop Runtime 10 required';
    document.querySelector('[data-release-summary]').textContent = `Windows 10 and 11 · x64 · Version ${release.version}`;
    ready.textContent = 'Ready'; ready.classList.remove('is-unavailable');
    status.textContent = `${release.size} · SHA-256 listed below`;
  } catch (error) {
    for (const link of links) { link.classList.add('is-disabled'); link.setAttribute('aria-disabled', 'true'); link.removeAttribute('download'); }
    ready.textContent = 'Unavailable'; ready.classList.add('is-unavailable');
    status.textContent = `${error.message} Please try again later.`;
    document.querySelector('[data-runtime]').textContent = 'See release notes when a package is available';
    document.querySelector('[data-package-sha]').textContent = 'Not published';
  }
}
loadRelease();
