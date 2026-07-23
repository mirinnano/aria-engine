import { StrictMode } from "react";
import { createRoot } from "react-dom/client";
import App from "./App";

if ("serviceWorker" in navigator && window.location.protocol.startsWith("http")) {
  void navigator.serviceWorker.register(new URL("./service-worker.js", document.baseURI));
}

createRoot(document.getElementById("root")!).render(
  <StrictMode><App /></StrictMode>,
);
