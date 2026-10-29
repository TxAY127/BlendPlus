import QtQuick
Item {
    id: limiter
    objectName: "__BlendPlusMenuLimiter"
    width: 0; height: 0
    function scan() {
        var queue = [parent]; var visited = 0;
        while (queue.length > 0 && visited++ < 16000) {
            var item = queue.pop();
            if (!item || item === limiter) continue;
            try {
                if (typeof item.maxVisibleItem !== "undefined" && typeof item.selectedIndex !== "undefined") {
                    var values = item.items;
                    if (values && values.length > 16) {
                        var blendMenu = false;
                        for (var j = 0; j < values.length; ++j) {
                            var label = String(values[j]);
                            if (label === "正片叠底" || label === "multiply_blend_mode" || label === "Multiply" || label === "差值") { blendMenu = true; break; }
                        }
                        if (blendMenu && item.maxVisibleItem !== 8) {
                            item.maxVisibleItem = 8;
                            console.info("BlendPlus menu: visible rows limited to 8");
                        }
                    }
                }
                var children = item.children;
                if (children) for (var i = 0; i < children.length; ++i) queue.push(children[i]);
            } catch (ignored) {}
        }
    }
    Timer { interval: 500; running: true; repeat: true; onTriggered: limiter.scan() }
}
