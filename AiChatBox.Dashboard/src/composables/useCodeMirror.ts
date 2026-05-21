import { watch, onBeforeUnmount, type Ref } from 'vue';
import { EditorView, basicSetup } from 'codemirror';
import { EditorState, type Extension } from '@codemirror/state';
import { oneDark } from '@codemirror/theme-one-dark';

// ── Shared base theme applied to every editor instance ───────────────────────
const baseTheme = EditorView.theme({
    '&': { borderRadius: '8px', overflow: 'hidden', fontSize: '0.82rem' },
    '.cm-scroller': { fontFamily: "'JetBrains Mono', 'Fira Code', monospace" },
    '.cm-content': { padding: '10px 0' }
});

// ─────────────────────────────────────────────────────────────────────────────

/**
 * Composable that mounts and manages a CodeMirror 6 editor inside `elRef`.
 *
 * @param elRef        - Template ref of the host element
 * @param modelValue   - Ref<string> bound to the editor's content (two-way)
 * @param extensions   - Language / extra extensions (e.g. `[sql()]`, `[json()]`)
 * @param options      - Optional: `readonly` flag and `maxHeight` CSS string
 *
 * @returns `{ mount, destroy }` — call `mount()` after the element is in the DOM.
 *
 * @example
 * const editorEl = ref<HTMLElement | null>(null);
 * const { mount, destroy } = useCodeMirror(editorEl, myStringRef, [sql()]);
 * watch(showEditor, async (v) => { if (v) { await nextTick(); mount(); } else destroy(); });
 */
export function useCodeMirror(
    elRef: Ref<HTMLElement | null>,
    modelValue: Ref<string>,
    extensions: Extension[],
    options: { readonly?: boolean; maxHeight?: string } = {}
) {
    let view: EditorView | null = null;

    function mount() {
        if (!elRef.value || view) return;

        const allExtensions: Extension[] = [
            basicSetup,
            ...extensions,
            oneDark,
            baseTheme,
        ];

        if (options.maxHeight) {
            allExtensions.push(
                EditorView.theme({
                    '.cm-scroller': { maxHeight: options.maxHeight, overflow: 'auto' }
                })
            );
        }

        if (options.readonly) {
            allExtensions.push(EditorState.readOnly.of(true));
        } else {
            allExtensions.push(
                EditorView.updateListener.of(update => {
                    if (update.docChanged) {
                        modelValue.value = update.state.doc.toString();
                    }
                })
            );
        }

        view = new EditorView({
            state: EditorState.create({ doc: modelValue.value, extensions: allExtensions }),
            parent: elRef.value
        });
    }

    function destroy() {
        view?.destroy();
        view = null;
    }

    // Keep editor in sync when value changes externally (e.g. auto-fill defaults)
    watch(modelValue, newVal => {
        if (!view) return;
        const current = view.state.doc.toString();
        if (current !== newVal) {
            view.dispatch({ changes: { from: 0, to: current.length, insert: newVal } });
        }
    });

    onBeforeUnmount(destroy);

    return { mount, destroy };
}
