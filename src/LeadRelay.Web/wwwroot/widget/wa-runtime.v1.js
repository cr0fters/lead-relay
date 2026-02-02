(function () {
    var cfg = window.__LeadRelayWidgetConfig;
    if (!cfg) return;

    if (window.__LeadRelayRuntimeLoaded) return;
    window.__LeadRelayRuntimeLoaded = true;

    function isMobile() {
        return /Android|iPhone|iPad|iPod|Mobile/i.test(navigator.userAgent || "");
    }

    function clamp(n, min, max, fallback) {
        if (!isFinite(n)) return fallback;
        return Math.min(max, Math.max(min, n));
    }

    function openWhatsApp(message) {
        var text = encodeURIComponent(message || cfg.prefill || "Hi");
        var url = "https://wa.me/" + (cfg.waNumber || "").replace(/[^\d]/g, "") + "?text=" + text;
        window.open(url, "_blank", "noopener,noreferrer");
    }

    function buildMessage() {
        return cfg.prefill || "Hi";
    }

    function makeLogoNode() {
        if (cfg.logoUrl) {
            var img = document.createElement("img");
            img.alt = "";
            img.setAttribute("aria-hidden", "true");
            img.src = cfg.logoUrl;
            img.decoding = "async";
            img.loading = "lazy";
            img.style.cssText = "width:20px;height:20px;display:block;";
            return img;
        }

        var badge = document.createElement("span");
        badge.setAttribute("aria-hidden", "true");
        badge.textContent = "WA";
        badge.style.cssText =
            "width:22px;height:22px;display:inline-flex;align-items:center;justify-content:center;" +
            "border-radius:999px;background:rgba(255,255,255,.18);color:#fff;font:700 11px/1 system-ui,-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Ubuntu,Arial,sans-serif;";
        return badge;
    }

    function makeButton() {
        var btn = document.createElement("button");
        btn.type = "button";
        btn.setAttribute("aria-label", cfg.label || "Chat via WhatsApp");

        var position = (cfg.position || "right").toLowerCase();
        var offset = clamp(parseInt(cfg.offset, 10), 0, 200, 24);
        var zIndex = clamp(parseInt(cfg.zIndex, 10), 1, 2147483647, 2147483000);

        btn.style.cssText =
            "position:fixed;" +
            "bottom:" + offset + "px;" +
            (position === "left" ? "left:" : "right:") + offset + "px;" +
            "z-index:" + zIndex + ";" +
            "display:flex;align-items:center;justify-content:center;gap:10px;" +
            "padding:12px 14px;border:0;border-radius:999px;cursor:pointer;" +
            "font:600 14px/1 system-ui,-apple-system,BlinkMacSystemFont,Segoe UI,Roboto,Ubuntu,Arial,sans-serif;" +
            "box-shadow:0 10px 25px rgba(0,0,0,.18);" +
            "background:" + (cfg.colour || "#25D366") + ";" +
            "color:#fff;" +
            "user-select:none;" +
            "transition:transform .12s ease, box-shadow .12s ease, opacity .12s ease;" +
            "max-width:min(90vw,340px);" +
            "white-space:nowrap;overflow:hidden;text-overflow:ellipsis;";

        btn.appendChild(makeLogoNode());

        var text = document.createElement("span");
        text.textContent = cfg.label || "Chat via WhatsApp";
        text.style.cssText = "overflow:hidden;text-overflow:ellipsis;";
        btn.appendChild(text);

        btn.addEventListener("mouseenter", function () {
            btn.style.transform = "translateY(-1px)";
            btn.style.boxShadow = "0 12px 28px rgba(0,0,0,.22)";
        });

        btn.addEventListener("mouseleave", function () {
            btn.style.transform = "translateY(0)";
            btn.style.boxShadow = "0 10px 25px rgba(0,0,0,.18)";
        });

        btn.addEventListener("mousedown", function () {
            btn.style.transform = "translateY(0) scale(.985)";
        });

        btn.addEventListener("mouseup", function () {
            btn.style.transform = "translateY(-1px)";
        });

        return btn;
    }

    var btn = makeButton();
    btn.disabled = true;
    btn.style.opacity = ".85";

    function mount() {
        if (!document.body) return;
        document.body.appendChild(btn);
    }

    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", mount, { once: true });
    else mount();

    var resolvedMessage = buildMessage();
    btn.disabled = false;
    btn.style.opacity = "1";

    btn.addEventListener("click", function (e) {
        e.preventDefault();
        return openWhatsApp(resolvedMessage);
    });
})();
