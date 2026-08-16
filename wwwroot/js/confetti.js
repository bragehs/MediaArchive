// A one-shot confetti burst. Appends its own full-screen canvas, animates, and
// removes itself — call window.confetti.burst() from .NET via JS interop.
window.confetti = (function () {
  const COLORS = ['#8bbe5a', '#d6a74a', '#ec8c58', '#e88cb4', '#8cc8e0', '#fafcf8'];

  function burst() {
    const canvas = document.createElement('canvas');
    canvas.style.cssText = 'position:fixed;inset:0;width:100%;height:100%;pointer-events:none;z-index:9999';
    document.body.appendChild(canvas);
    const ctx = canvas.getContext('2d');
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    const W = window.innerWidth, H = window.innerHeight;
    canvas.width = W * dpr; canvas.height = H * dpr; ctx.scale(dpr, dpr);

    const parts = [];
    for (let i = 0; i < 150; i++) {
      const a = Math.random() * Math.PI * 2;
      const sp = 4 + Math.random() * 9;
      parts.push({
        x: W / 2, y: H * 0.4,
        vx: Math.cos(a) * sp, vy: Math.sin(a) * sp - 6,
        s: 5 + Math.random() * 6, rot: Math.random() * 6.28, vr: (Math.random() - 0.5) * 0.3,
        col: COLORS[i % COLORS.length], life: 0, max: 90 + Math.random() * 45
      });
    }

    let frame = 0;
    function tick() {
      frame++;
      ctx.clearRect(0, 0, W, H);
      let alive = 0;
      for (const p of parts) {
        if (p.life > p.max) continue;
        p.life++; alive++;
        p.vy += 0.28; p.vx *= 0.99;
        p.x += p.vx; p.y += p.vy; p.rot += p.vr;
        const fade = p.life > p.max - 22 ? (p.max - p.life) / 22 : 1;
        ctx.save();
        ctx.globalAlpha = Math.max(0, fade);
        ctx.translate(p.x, p.y); ctx.rotate(p.rot);
        ctx.fillStyle = p.col;
        ctx.fillRect(-p.s / 2, -p.s / 2, p.s, p.s * 0.62);
        ctx.restore();
      }
      if (alive > 0 && frame < 260) requestAnimationFrame(tick);
      else canvas.remove();
    }
    requestAnimationFrame(tick);
  }

  return { burst };
})();
