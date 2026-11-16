import React from "react";
import ReactDOM from "react-dom/client";
import "@xyflow/react/dist/style.css";
import "@/styles/globals.css";
import { RiskCaseApp } from "@/app";

ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <RiskCaseApp />
  </React.StrictMode>,
);
