import React from "react";
import { getUser } from "./lib/api";
import ReactDOM from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import "bootstrap/dist/css/bootstrap.min.css";
import "./styles.css";
import { Preferences } from "./lib/context";
import { Layout } from "./Layout";
import { ErrorPage } from "./pages/ErrorPage";
const Positions = React.lazy(() =>
  import("./pages/Lists").then((m) => ({ default: m.Positions })),
);
const Attributes = React.lazy(() =>
  import("./pages/Lists").then((m) => ({ default: m.Attributes })),
);
const PositionEditor = React.lazy(() =>
  import("./pages/Builders").then((m) => ({ default: m.PositionEditor })),
);
const AttributeEditor = React.lazy(() =>
  import("./pages/Builders").then((m) => ({ default: m.AttributeEditor })),
);
const PositionPage = React.lazy(() =>
  import("./pages/Details").then((m) => ({ default: m.PositionPage })),
);
const PositionCVGallery = React.lazy(() =>
  import("./pages/PositionDetails").then((m) => ({
    default: m.PositionCVGallery,
  })),
);
const CVPage = React.lazy(() =>
  import("./pages/Details").then((m) => ({ default: m.CVPage })),
);
const ProfilePage = React.lazy(() =>
  import("./pages/Profile").then((m) => ({ default: m.ProfilePage })),
);
const Auth = React.lazy(() =>
  import("./pages/Auth").then((m) => ({ default: m.Auth })),
);
const CheckEmail = React.lazy(() => import("./pages/CheckEmail"));
const Admin = React.lazy(() =>
  import("./pages/Auth").then((m) => ({ default: m.Admin })),
);
const router = createBrowserRouter([
  {
    element: <Layout />,
    errorElement: <ErrorPage status={500} />,
    children: [
      { index: true, element: <Positions /> },
      { path: "positions/new", element: <PositionEditor key="new" /> },
      { path: "positions/:id/edit", element: <PositionEditor /> },
      { path: "positions/:id/cvs", element: <PositionCVGallery /> },
      { path: "positions/:id", element: <PositionPage /> },
      { path: "attributes", element: <Attributes /> },
      { path: "attributes/new", element: <AttributeEditor key="new" /> },
      { path: "attributes/:id", element: <AttributeEditor /> },
      { path: "profile", element: <ProfilePage /> },
      { path: "profiles/:id", element: <ProfilePage /> },
      { path: "cvs/:id", element: <CVPage /> },
      { path: "check-email", element: <CheckEmail /> },
      { path: "login", element: <Auth /> },
      { path: "admin", element: <Admin /> },
      { path: "errors/:status", element: <ErrorPage /> },
      { path: "*", element: <ErrorPage status={404} /> },
    ],
  },
]);
const client = new QueryClient({
  defaultOptions: {
    queries: { retry: false, staleTime: 15000, refetchOnWindowFocus: false },
  },
});
let sessionIdentity = getUser()?.id;
window.addEventListener("session", () => {
  const next = getUser()?.id;
  if (next !== sessionIdentity) {
    client.clear();
    sessionIdentity = next;
  }
});
ReactDOM.createRoot(document.getElementById("root")!).render(
  <React.StrictMode>
    <QueryClientProvider client={client}>
      <Preferences>
        <RouterProvider router={router} />
      </Preferences>
    </QueryClientProvider>
  </React.StrictMode>,
);
