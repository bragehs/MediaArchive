// Whether a line-clamped element is actually hiding anything, so a "show more"
// only appears when there is more. The 1px allows for sub-pixel line heights.
window.blurb = {
  overflows: el => !!el && el.scrollHeight > el.clientHeight + 1
};
