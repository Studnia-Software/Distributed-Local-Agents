import { useState } from "react";
import { useMutation } from "@tanstack/react-query";

const defaultPrompt = "Napisz krotkie powitanie dla zespolu.";
const defaultModel = "qwen2.5:0.5b";

async function readError(response) {
  const body = await response.text();
  try {
    const json = JSON.parse(body);
    return json.error?.message ?? body;
  } catch {
    return body || `${response.status} ${response.statusText}`;
  }
}

async function completePrompt({ model, prompt, stream }) {
  const response = await fetch("/api/v1/tasks", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      task_id: crypto.randomUUID(),
      stage: 0,
      start_layer: 0,
      end_layer: 1,
      model,
      payload: prompt
    })
  });

  if (!response.ok) throw new Error(await readError(response));
  const json = await response.json();
  return { content: json.payload ?? "", streamed: false };
}

function App() {
  const [model, setModel] = useState(defaultModel);
  const [prompt, setPrompt] = useState(defaultPrompt);
  const [stream, setStream] = useState(false);
  const [answer, setAnswer] = useState("");
  const [copied, setCopied] = useState(false);

  const completion = useMutation({
    mutationFn: completePrompt,
    onSuccess: (result) => setAnswer(result.content)
  });

  const submit = (event) => {
    event.preventDefault();
    setCopied(false);
    completion.mutate({ model: model.trim(), prompt: prompt.trim(), stream });
  };

  const copyAnswer = async () => {
    await navigator.clipboard.writeText(answer);
    setCopied(true);
  };

  const status = completion.isPending ? "generowanie..." : completion.isError ? "blad" : "gotowy";

  return (
    <main className="shell">
      <header className="topbar">
        <div>
          <p className="eyebrow">LOCAL MODEL CONSOLE</p>
          <h1>axe_ai <span>/ prompt lab</span></h1>
        </div>
        <div className={`status status-${completion.isError ? "error" : completion.isPending ? "working" : "idle"}`}>
          <span className="status-dot" /> {status}
        </div>
      </header>

      <section className="workspace">
        <form className="composer" onSubmit={submit}>
          <div className="field-row">
            <label>Model<input value={model} onChange={(event) => setModel(event.target.value)} required /></label>
            <div className="nodes-card">
              <span className="eyebrow">NODES</span>
              <strong>1</strong>
              <small>lokalny</small>
            </div>
          </div>
          <label>Prompt<textarea value={prompt} onChange={(event) => setPrompt(event.target.value)} rows="9" required /></label>
          <div className="actions">
            <label className="toggle"><input type="checkbox" checked={stream} onChange={(event) => setStream(event.target.checked)} /> stream</label>
            <button className="button button-quiet" type="button" onClick={() => { setPrompt(""); setAnswer(""); }}>Wyczysc</button>
            <button className="button button-primary" type="submit" disabled={completion.isPending || !prompt.trim()}>
              {completion.isPending ? "Czekaj..." : "Wyslij prompt ->"}
            </button>
          </div>
        </form>

        <section className="response-panel" aria-live="polite">
          <div className="panel-heading"><div><p className="eyebrow">RESPONSE</p><h2>Odpowiedz modelu</h2></div>
            <button className="icon-button" type="button" onClick={copyAnswer} disabled={!answer}>{copied ? "skopiowano" : "kopiuj"}</button>
          </div>
          <div className={`response ${answer ? "" : "empty"}`}>
            {completion.isError ? completion.error.message : answer || "Odpowiedz pojawi sie tutaj."}
          </div>
        </section>
      </section>
      <footer><span>TanStack Query · OpenAI-compatible API</span><span>Proxy: /api -&gt; localhost:5175</span></footer>
    </main>
  );
}

export default App;