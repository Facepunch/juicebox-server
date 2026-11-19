import React from "react";
import { ThemeProvider } from "styled-components";
import { defaultTheme } from "../../styles/theme";
import styled from "styled-components";

import GlobalStyle from "../../styles/global";
import Header from "../../components/Header";

const Body = styled.div`
    align-items: center;
    justify-content: center;
    display: flex;
    flex-direction: column;
    width: 100%;
    flex-grow: 1;
    padding: 0 2rem 2rem 2rem;
`;
interface Props {
    children?: React.ReactNode;
}

const Layout: React.FC<Props> = ({ children }) => {
    return (
        <>
            <GlobalStyle />
            <ThemeProvider theme={defaultTheme}>
                <Header />
                <Body>{children}</Body>
            </ThemeProvider>
        </>
    );
};

export default Layout;
