const webVNStorage = (() => {
  const dbName = "webvn-editor";
  const storeName = "kv";

  function openDb() {
    return new Promise((resolve, reject) => {
      const request = window.indexedDB.open(dbName, 1);

      request.onupgradeneeded = function () {
        const db = request.result;
        if (!db.objectStoreNames.contains(storeName)) {
          db.createObjectStore(storeName);
        }
      };

      request.onsuccess = function () {
        resolve(request.result);
      };

      request.onerror = function () {
        reject(request.error);
      };
    });
  }

  async function getAsync(key) {
    const db = await openDb();

    try {
      const value = await new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readonly");
        const store = transaction.objectStore(storeName);
        const request = store.get(key);

        request.onsuccess = function () {
          resolve(request.result ?? null);
        };

        request.onerror = function () {
          reject(request.error);
        };
      });

      if (value !== null) {
        return value;
      }

      const legacyValue = window.localStorage.getItem(key);
      if (legacyValue) {
        await setAsync(key, legacyValue);
        window.localStorage.removeItem(key);
      }

      return legacyValue;
    } finally {
      db.close();
    }
  }

  async function setAsync(key, value) {
    const db = await openDb();

    try {
      await new Promise((resolve, reject) => {
        const transaction = db.transaction(storeName, "readwrite");
        const store = transaction.objectStore(storeName);
        const request = store.put(value, key);

        request.onsuccess = function () {
          resolve();
        };

        request.onerror = function () {
          reject(request.error);
        };
      });
    } finally {
      db.close();
    }
  }

  return {
    getAsync,
    setAsync
  };
})();

const webVNDrag = (() => {
  let activeDrag = null;

  function clamp(value, min, max) {
    return Math.min(max, Math.max(min, value));
  }

  function getHandleTransformFactors(characterElement) {
    const styles = window.getComputedStyle(characterElement);
    const scaleValue = Number.parseFloat(styles.getPropertyValue("--character-scale"));
    const flipValue = Number.parseFloat(styles.getPropertyValue("--character-flip"));

    return {
      scale: Number.isFinite(scaleValue) && scaleValue !== 0 ? Math.abs(scaleValue) : 1,
      flip: Number.isFinite(flipValue) && flipValue < 0 ? -1 : 1
    };
  }

  function applyCharacterPreview(state) {
    const left = 50 + (state.currentX * 40);
    const bottom = 10 + (state.currentY * 40);
    state.characterElement.style.left = `${left}%`;
    state.characterElement.style.bottom = `${bottom}%`;
    state.characterElement.style.setProperty("--character-scale", `${state.currentScale}`);
    state.characterElement.style.setProperty("--handle-scale", `${1 / Math.max(0.1, state.currentScale)}`);
  }

  function clampHandlesIntoStage(state) {
    const handleAnchor = state.characterElement.querySelector(".character-handle-anchor");
    if (!handleAnchor) {
      return;
    }

    state.characterElement.style.setProperty("--handle-offset-x", "0px");
    state.characterElement.style.setProperty("--handle-offset-y", "0px");

    const margin = 8;
    const handleRect = handleAnchor.getBoundingClientRect();
    const stageRect = state.stageRect;

    let offsetX = 0;
    let offsetY = 0;

    if (handleRect.left < stageRect.left + margin) {
      offsetX = (stageRect.left + margin) - handleRect.left;
    } else if (handleRect.right > stageRect.right - margin) {
      offsetX = (stageRect.right - margin) - handleRect.right;
    }

    if (handleRect.top < stageRect.top + margin) {
      offsetY = (stageRect.top + margin) - handleRect.top;
    } else if (handleRect.bottom > stageRect.bottom - margin) {
      offsetY = (stageRect.bottom - margin) - handleRect.bottom;
    }

    const transform = getHandleTransformFactors(state.characterElement);
    const localOffsetX = offsetX === 0 ? 0 : offsetX / (transform.scale * transform.flip);
    const localOffsetY = offsetY === 0 ? 0 : offsetY / transform.scale;

    state.characterElement.style.setProperty("--handle-offset-x", `${localOffsetX}px`);
    state.characterElement.style.setProperty("--handle-offset-y", `${localOffsetY}px`);
  }

  function clampCharacterHandles(stageElement, characterElementId) {
    const characterElement = document.getElementById(characterElementId);
    if (!stageElement || !characterElement) {
      return;
    }

    clampHandlesIntoStage({
      stageRect: stageElement.getBoundingClientRect(),
      characterElement
    });
  }

  function computeState(state, clientX, clientY) {
    const deltaX = clientX - state.startClientX;
    const deltaY = clientY - state.startClientY;
    const stageWidth = Math.max(1, state.stageRect.width);
    const stageHeight = Math.max(1, state.stageRect.height);

    if (state.mode === "translate") {
      state.currentX = state.startX + ((deltaX / stageWidth) * 2.5);
      state.currentY = state.startY - ((deltaY / stageHeight) * 2.0);
      return;
    }

    state.currentScale = Math.max(0.1, state.startScale + ((deltaX - deltaY) / 300));
  }

  function queuePreviewUpdate(clientX, clientY) {
    if (!activeDrag) {
      return;
    }

    activeDrag.lastClientX = clientX;
    activeDrag.lastClientY = clientY;

    if (activeDrag.frameHandle !== null) {
      return;
    }

    activeDrag.frameHandle = window.requestAnimationFrame(() => {
      if (!activeDrag) {
        return;
      }

      activeDrag.frameHandle = null;
      computeState(activeDrag, activeDrag.lastClientX, activeDrag.lastClientY);
      applyCharacterPreview(activeDrag);
    });
  }

  async function finishDrag(clientX, clientY) {
    if (!activeDrag) {
      return;
    }

    const state = activeDrag;
    activeDrag = null;

    window.removeEventListener("pointermove", state.onPointerMove, true);
    window.removeEventListener("pointerup", state.onPointerUp, true);
    window.removeEventListener("pointercancel", state.onPointerUp, true);

    if (state.frameHandle !== null) {
      window.cancelAnimationFrame(state.frameHandle);
    }

    computeState(state, clientX, clientY);
    applyCharacterPreview(state);
    clampHandlesIntoStage(state);

    await state.dotNetRef.invokeMethodAsync("CompleteStageDrag", state.characterId, state.currentX, state.currentY, state.currentScale);
  }

  async function startCharacterDrag(stageElement, characterElementId, characterId, mode, startClientX, startClientY, startX, startY, startScale, flipX, dotNetRef) {
    if (activeDrag) {
      await finishDrag(activeDrag.lastClientX, activeDrag.lastClientY);
    }

    const characterElement = document.getElementById(characterElementId);
    if (!stageElement || !characterElement) {
      return;
    }

    const stageRect = stageElement.getBoundingClientRect();
    activeDrag = {
      stageRect,
      characterElement,
      characterId,
      mode,
      startClientX,
      startClientY,
      lastClientX: startClientX,
      lastClientY: startClientY,
      startX,
      startY,
      startScale,
      currentX: startX,
      currentY: startY,
      currentScale: startScale,
      flipX,
      dotNetRef,
      frameHandle: null,
      onPointerMove: event => {
        event.preventDefault();
        queuePreviewUpdate(event.clientX, event.clientY);
      },
      onPointerUp: async event => {
        event.preventDefault();
        await finishDrag(event.clientX, event.clientY);
      }
    };

    characterElement.style.setProperty("--character-flip", flipX ? "-1" : "1");
    characterElement.style.setProperty("--handle-offset-x", "0px");
    characterElement.style.setProperty("--handle-offset-y", "0px");
    applyCharacterPreview(activeDrag);

    window.addEventListener("pointermove", activeDrag.onPointerMove, true);
    window.addEventListener("pointerup", activeDrag.onPointerUp, true);
    window.addEventListener("pointercancel", activeDrag.onPointerUp, true);
  }

  return {
    startCharacterDrag,
    clampCharacterHandles
  };
})();

const webVNPicking = (() => {
  const imageDataCache = new Map();

  async function getImageDataForElement(imageElement) {
    const source = imageElement.currentSrc || imageElement.src;
    if (!source) {
      return null;
    }

    if (imageDataCache.has(source)) {
      return imageDataCache.get(source);
    }

    const image = await new Promise((resolve, reject) => {
      if (imageElement.complete && imageElement.naturalWidth > 0) {
        resolve(imageElement);
        return;
      }

      const handleLoad = () => {
        cleanup();
        resolve(imageElement);
      };

      const handleError = error => {
        cleanup();
        reject(error);
      };

      const cleanup = () => {
        imageElement.removeEventListener("load", handleLoad);
        imageElement.removeEventListener("error", handleError);
      };

      imageElement.addEventListener("load", handleLoad, { once: true });
      imageElement.addEventListener("error", handleError, { once: true });
    });

    const width = image.naturalWidth || image.width;
    const height = image.naturalHeight || image.height;
    if (!width || !height) {
      return null;
    }

    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext("2d", { willReadFrequently: true });
    if (!context) {
      return null;
    }

    context.clearRect(0, 0, width, height);
    context.drawImage(image, 0, 0, width, height);
    const imageData = context.getImageData(0, 0, width, height);
    const payload = { width, height, data: imageData.data };
    imageDataCache.set(source, payload);
    return payload;
  }

  async function pickCharacterAtPoint(stageElement, clientX, clientY) {
    if (!stageElement) {
      return null;
    }

    const stageRect = stageElement.getBoundingClientRect();
    if (clientX < stageRect.left || clientX > stageRect.right || clientY < stageRect.top || clientY > stageRect.bottom) {
      return null;
    }

    const candidates = Array
      .from(stageElement.querySelectorAll(".scene-character"))
      .sort((left, right) => {
        const leftZ = Number.parseInt(window.getComputedStyle(left).zIndex || "0", 10);
        const rightZ = Number.parseInt(window.getComputedStyle(right).zIndex || "0", 10);
        return rightZ - leftZ;
      });

    for (const characterElement of candidates) {
      const characterId = characterElement.getAttribute("data-character-id");
      if (!characterId) {
        continue;
      }

      const imageElement = characterElement.querySelector(".scene-character-image");
      if (!imageElement) {
        const rect = characterElement.getBoundingClientRect();
        if (clientX >= rect.left && clientX <= rect.right && clientY >= rect.top && clientY <= rect.bottom) {
          return characterId;
        }

        continue;
      }

      const rect = imageElement.getBoundingClientRect();
      if (clientX < rect.left || clientX > rect.right || clientY < rect.top || clientY > rect.bottom) {
        continue;
      }

      const payload = await getImageDataForElement(imageElement);
      if (!payload) {
        return characterId;
      }

      const xRatio = rect.width <= 0 ? 0 : (clientX - rect.left) / rect.width;
      const yRatio = rect.height <= 0 ? 0 : (clientY - rect.top) / rect.height;

      const styles = window.getComputedStyle(characterElement);
      const flipValue = Number.parseFloat(styles.getPropertyValue("--character-flip"));
      const flipped = Number.isFinite(flipValue) && flipValue < 0;

      const normalizedX = flipped ? 1 - xRatio : xRatio;
      const pixelX = Math.max(0, Math.min(payload.width - 1, Math.floor(normalizedX * payload.width)));
      const pixelY = Math.max(0, Math.min(payload.height - 1, Math.floor(yRatio * payload.height)));
      const alpha = payload.data[((pixelY * payload.width) + pixelX) * 4 + 3];

      if (alpha > 8) {
        return characterId;
      }
    }

    return null;
  }

  return {
    pickCharacterAtPoint
  };
})();

window.webVN = {
  storage: webVNStorage,
  pickCharacterAtPoint: webVNPicking.pickCharacterAtPoint,
  downloadJson: function (fileName, json) {
    const blob = new Blob([json], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  },
  downloadBytes: function (fileName, bytes, contentType) {
    const blob = new Blob([bytes], { type: contentType || "application/octet-stream" });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = fileName;
    link.click();
    URL.revokeObjectURL(url);
  },
  startCharacterDrag: webVNDrag.startCharacterDrag,
  clampCharacterHandles: webVNDrag.clampCharacterHandles
};
