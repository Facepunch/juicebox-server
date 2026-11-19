import styled from 'styled-components';

export const FieldContainer = styled.div`
    display: flex;
    flex-direction: column;
    flex-grow: 1;
    margin-bottom: 1rem;

    &:last-of-type {
        margin-bottom: 0;
    }

    .field-body {
        display: flex;
        flex-grow: 1;
    }

    .input {
        color: ${props => props.theme.colors.textLight};
        background-color: transparent;
        border-radius: ${props => props.theme.borderRadius};
        border-width: 2px;
        border-style: solid;
        border-color: ${props => props.theme.colors.border};
        font-size: 1rem;
        font-weight: 600;
        min-height: 48px;
        padding: 1rem;
        flex-grow: 1;
        transition: ${props => props.theme.transitionDefault};

        &:focus, &:active {
            background-color: ${props => `${props.theme.colors.primary}10`};
            border-color: ${props => props.theme.colors.primary};
        }

        &::placeholder {
        color: ${props => props.theme.colors.textLight};
        opacity: 0.3;
        }

        &.textarea {
            resize: none;
        }

        &.is-active {
            border-color: ${props => props.theme.colors.primary};
        }

        &.has-error {
            background-color: ${(props) => `${props.theme.colors.orange}20`};
            border: 2px solid ${(props) => `${props.theme.colors.orange}50`};
        }
    }

    .radio {
        align-items: center;
        color: ${props => props.theme.colors.textLight};
        background-color: transparent;
        border-radius: ${props => props.theme.borderRadius};
        border-width: 2px;
        border-style: solid;
        border-color: ${props => props.theme.colors.border};
        cursor: pointer;
        font-size: 1rem;
        font-weight: 600;
        min-height: 48px;
        padding: 1rem;
        flex-grow: 1;
        transition: ${props => props.theme.transitionDefault};
        display: flex;

        &:hover {
            background-color: ${props => `${props.theme.colors.primary}10`};
            border-color: ${props => props.theme.colors.primary};

            .radioIcon {
                color: ${props => props.theme.colors.primary};
            }
        }

        &.is-toggled {
            background-color: ${props => `${props.theme.colors.primary}10`};
            border-color: ${props => props.theme.colors.primary};

            .radioIcon {
                color: ${props => props.theme.colors.primary};
            }

            label {
                color: #fff;
            }
        }

        label {
            color: ${props => props.theme.colors.textLight};
            cursor: pointer;
            flex-grow: 1;
            transition: ${props => props.theme.transitionDefault};
        }

        .radioIcon {
            color: ${props => props.theme.colors.border};
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 24px;
            transition: ${props => props.theme.transitionDefault};
        }

    }
`