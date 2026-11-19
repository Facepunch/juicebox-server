import styled from "styled-components";

export const StyledButton = styled.button`
    background-color: ${(props) => props.theme.colors.primary};
    border-radius: 30px;
    border: none;
    box-shadow: 0 5px 10px rgba(0, 0, 0, 0.1);
    color: #fff;
    cursor: pointer;
    display: flex;
    font-size: 1rem;
    font-family: "Poppins", sans-serif;
    font-weight: 700;
    flex-grow: 1;
    padding: 0.75rem;
    width: 100%;
    text-align: center;
    text-shadow: 0 1px 0 rgba(0, 0, 0, 0.2);
    align-items: center;
    justify-content: center;
    transition: ${(props) => props.theme.transitionDefault};
    position: relative;
    top: 0;

    &:not(:first-child) {
        margin-top: 2rem;
    }

    &:hover {
        background-color: ${(props) => `${props.theme.colors.primary}80`};
        box-shadow: 0 10px 20px rgba(0, 0, 0, 0.5);
        top: -2px;
    }

    &:disabled {
        cursor: not-allowed;
        opacity: 0.3;

        &:hover {
            background-color: ${(props) => props.theme.colors.primary};
            top: 0;
        }
    }
`;
