import React, { useMemo } from "react";
import ReactDOM from "react-dom/client";
import { createBrowserRouter, RouterProvider } from "react-router-dom";
import { GameModel, GameModelProvider } from "./models/GameModel";

import Login from "./routes/login";
import Play from "./routes/play";

const root = ReactDOM.createRoot(
    document.getElementById("root") as HTMLElement
);

const router = createBrowserRouter([
    {
        path: "/",
        element: <Login />
    },
    {
        path: "/play",
        element: <Play />
    }
]);

function RootComponent() {
    const gameModel = useMemo(() => new GameModel(), []);
    return (
        <GameModelProvider value={gameModel}>
            <RouterProvider router={router} />
        </GameModelProvider>
    );
}

root.render(
    <React.StrictMode>
        <RootComponent />
    </React.StrictMode>
);
